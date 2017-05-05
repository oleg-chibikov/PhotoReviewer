using System;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Common.Logging;
using GalaSoft.MvvmLight.Messaging;
using JetBrains.Annotations;
using PhotoReviewer.Contracts.View;
using PhotoReviewer.Core;
using PhotoReviewer.Resources;
using PropertyChanged;
using Scar.Common.Drawing.ExifTool;
using Scar.Common.Drawing.ImageRetriever;
using Scar.Common.Drawing.Metadata;
using Scar.Common.IO;
using Scar.Common.WPF.Commands;

namespace PhotoReviewer.ViewModel
{
    public enum ChangeType
    {
        Reload,
        Next,
        Prev
    }

    [ImplementPropertyChanged]
    public sealed class PhotoViewModel
    {
        [NotNull] private readonly IExifTool _exifTool;
        [NotNull] private readonly IImageRetriever _imageRetriever;

        [NotNull] private readonly ILog _logger;

        [NotNull] private readonly MainViewModel _mainViewModel;

        [NotNull] private readonly IMessenger _messenger;

        [NotNull] private readonly ICancellationTokenSourceProvider _mainOperationsCancellationTokenSourceProvider;

        public PhotoViewModel([NotNull] Photo photo, [NotNull] MainViewModel mainViewModel,
            [NotNull] WindowsArranger windowsArranger, [NotNull] ILog logger, [NotNull] IExifTool exifTool,
            [NotNull] IMessenger messenger,
            [NotNull] ICancellationTokenSourceProvider mainOperationsCancellationTokenSourceProvider,
            [NotNull] IImageRetriever imageRetriever)
        {
            _imageRetriever = imageRetriever ?? throw new ArgumentNullException(nameof(imageRetriever));
            if (windowsArranger == null)
                throw new ArgumentNullException(nameof(windowsArranger));

            _mainOperationsCancellationTokenSourceProvider = mainOperationsCancellationTokenSourceProvider ?? throw new ArgumentNullException(nameof(mainOperationsCancellationTokenSourceProvider));
            _mainViewModel = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _exifTool = exifTool ?? throw new ArgumentNullException(nameof(exifTool));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            Photo = photo ?? throw new ArgumentNullException(nameof(photo));
            ChangePhotoAsync(ChangeType.Reload);
            ToggleFullHeightCommand = new CorrelationCommand<IPhotoWindow>(windowsArranger.ToggleFullHeight);
            FavoriteCommand = new CorrelationCommand(Favorite);
            MarkForDeletionCommand = new CorrelationCommand(MarkForDeletion);
            RenameToDateCommand = new CorrelationCommand(RenameToDate);
            OpenPhotoInExplorerCommand = new CorrelationCommand(OpenPhotoInExplorer);
            ChangePhotoCommand = new CorrelationCommand<ChangeType>(ChangePhotoAsync);
            RotateCommand = new CorrelationCommand<RotationType>(RotateAsync);
        }

        private void SelectAndAct([NotNull] Action action)
        {
            _mainViewModel.SelectedPhoto = Photo;
            action();
        }

        #region Dependency Properties

        [CanBeNull]
        public BitmapSource BitmapSource { get; set; }

        [NotNull]
        public Photo Photo { get; set; }

        #endregion

        #region Commands

        public ICommand ToggleFullHeightCommand { get; }
        public ICommand FavoriteCommand { get; }
        public ICommand MarkForDeletionCommand { get; }
        public ICommand OpenPhotoInExplorerCommand { get; }
        public ICommand RenameToDateCommand { get; }
        public ICommand ChangePhotoCommand { get; }
        public ICommand RotateCommand { get; }

        #endregion

        #region Command Handlers

        private async void ChangePhotoAsync(ChangeType changeType)
        {
            Photo newPhoto;
            switch (changeType)
            {
                case ChangeType.Reload:
                    newPhoto = Photo;
                    break;
                case ChangeType.Next:
                    newPhoto = Photo.Next;
                    break;
                case ChangeType.Prev:
                    newPhoto = Photo.Prev;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(changeType), changeType, null);
            }

            if (newPhoto == null)
                return;

            newPhoto.ReloadCollectionInfoIfNeeded();
            //ViewedPhoto.Source = null;

            await _mainOperationsCancellationTokenSourceProvider.StartNewTask(token =>
            {
                Photo = newPhoto;
                const int previewWidth = 800;
                var filePath = newPhoto.FilePath;
                var orientation = newPhoto.Metadata.Orientation;
                SelectAndAct(async () =>
                {
                    try
                    {
                        var bytes = await filePath.ReadFileAsync(token);
                        //Firstly load and display low quality image
                        BitmapSource = await _imageRetriever.LoadImageAsync(bytes, token, orientation, previewWidth);
                        //Then load full image
                        BitmapSource = await _imageRetriever.LoadImageAsync(bytes, token, orientation);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception ex)
                    {
                        string message = $"Cannot load image {filePath}";
                        _logger.Warn(message, ex);
                        _messenger.Send(message, MessengerTokens.UserErrorToken);
                    }
                });
            }, true, true);
        }

        private async void RotateAsync(RotationType rotationType)
        {
            //TODO: Perform  physical rotation when no metadata
            _logger.Info($"Rotating {Photo} {rotationType}...");
            //Pass photoCollection cancellation token instead of photo's one (need to cancel these operations only if switching collection path)
            Photo.Metadata.Orientation = Photo.Metadata.Orientation.GetNextOrientation(rotationType);
            Photo.SetThumbnailAsync(_mainViewModel.PhotoCollection.MainOperationCancellationToken);
            Photo.ReloadMetadata();
            ChangePhotoAsync(ChangeType.Reload);
            string error;
            try
            {
                error = await _exifTool.SetOrientationAsync(Photo.Metadata.Orientation, Photo.FilePath, false,
                    _mainViewModel.PhotoCollection.MainOperationCancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.Warn("Rotation is cancelled");
                return;
            }

            if (error != null)
            {
                _messenger.Send(error, MessengerTokens.UserWarningToken);
                _logger.Warn($"{Photo} is not rotated {rotationType}: {error}");
            }
            else
            {
                _logger.Info($"{Photo} is rotated {rotationType}");
            }
        }

        private void Favorite()
        {
            SelectAndAct(() => _mainViewModel.Favorite());
        }

        private void MarkForDeletion()
        {
            SelectAndAct(() => _mainViewModel.MarkForDeletion());
        }

        private void RenameToDate()
        {
            SelectAndAct(() => _mainViewModel.RenameToDateAsync());
        }

        private void OpenPhotoInExplorer()
        {
            Photo.FilePath.OpenFileInExplorer();
        }

        #endregion
    }
}