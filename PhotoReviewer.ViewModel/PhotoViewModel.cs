using System;
using System.Threading;
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
    [AddINotifyPropertyChangedInterface]
    [UsedImplicitly]
    public sealed class PhotoViewModel
    {
        [NotNull]
        private readonly ICancellationTokenSourceProvider _cancellationTokenSourceProvider;

        [NotNull]
        private readonly IExifTool _exifTool;

        [NotNull]
        private readonly IImageRetriever _imageRetriever;

        [NotNull]
        private readonly ILog _logger;

        [NotNull]
        private readonly MainViewModel _mainViewModel;

        [NotNull]
        private readonly IMessenger _messenger;

        public PhotoViewModel(
            [NotNull] Photo photo,
            [NotNull] MainViewModel mainViewModel,
            [NotNull] WindowsArranger windowsArranger,
            [NotNull] ILog logger,
            [NotNull] IExifTool exifTool,
            [NotNull] IMessenger messenger,
            [NotNull] ICancellationTokenSourceProvider cancellationTokenSourceProvider,
            [NotNull] IImageRetriever imageRetriever)
        {
            _imageRetriever = imageRetriever ?? throw new ArgumentNullException(nameof(imageRetriever));
            if (windowsArranger == null)
                throw new ArgumentNullException(nameof(windowsArranger));

            _cancellationTokenSourceProvider = cancellationTokenSourceProvider ?? throw new ArgumentNullException(nameof(cancellationTokenSourceProvider));
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
            RotateCommand = new CorrelationCommand<RotationType>(Rotate);
            WindowClosingCommand = new CorrelationCommand(WindowClosing);
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
        public ICommand WindowClosingCommand { get; }

        #endregion

        #region Command Handlers

        private async void ChangePhotoAsync(ChangeType changeType)
        {
            Photo newPhoto;
            switch (changeType)
            {
                case ChangeType.None:
                    return;
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

            await _cancellationTokenSourceProvider.StartNewTask(token => LoadPhotoInternal(token, newPhoto)).ConfigureAwait(false);
        }

        private async void LoadPhotoInternal(CancellationToken token, [NotNull] Photo photo)
        {
            _mainViewModel.SelectedPhoto = Photo;
            photo.ReloadCollectionInfoIfNeeded();
            Photo = photo;
            const int previewWidth = 800;
            var filePath = photo.FilePath;
            var orientation = photo.Metadata.Orientation;
            try
            {
                var bytes = await filePath.ReadFileAsync(token).ConfigureAwait(false);
                //Firstly load and display low quality image
                BitmapSource = await _imageRetriever.LoadImageAsync(bytes, token, orientation, previewWidth).ConfigureAwait(false);
                //Then load full image
                BitmapSource = await _imageRetriever.LoadImageAsync(bytes, token, orientation).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                var message = $"Cannot load image {filePath}";
                _logger.Warn(message, ex);
                _messenger.Send(message, MessengerTokens.UserErrorToken);
            }
        }

        private void Rotate(RotationType rotationType)
        {
            _logger.Info($"Rotating {Photo} {rotationType}...");

            if (!Photo.OrientationIsSpecified)
            {
                _logger.Warn($"Orientation is not specified for {Photo}");
                _messenger.Send(Errors.NoMetadata, MessengerTokens.UserWarningToken);
                return;
            }
            var originalOrientation = Photo.Metadata.Orientation;
            Photo.Metadata.Orientation = originalOrientation.GetNextOrientation(rotationType);
            var angle = rotationType == RotationType.Clockwise
                ? 90
                : -90;
            _cancellationTokenSourceProvider.StartNewTask(
                async token =>
                {
                    try
                    {
                        //no need to cancel this operation (if rotation starts it should be finished)
                        var task = _exifTool.SetOrientationAsync(Photo.Metadata.Orientation, Photo.FilePath, false, token);
                        RotatePhoto(angle);
                        await task.ConfigureAwait(false);
                        _logger.Info($"{Photo} is rotated {rotationType}");
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.Warn("Rotation is cancelled");
                    }
                    catch (InvalidOperationException ex)
                    {
                        _messenger.Send(Errors.RotationFailed, MessengerTokens.UserWarningToken);
                        _logger.Warn("Rotation failed", ex);
                        Photo.Metadata.Orientation = originalOrientation;
                        RotatePhoto(-angle);
                    }
                });
        }

        private void RotatePhoto(int angle)
        {
            BitmapSource = _imageRetriever.ApplyRotateTransform(angle, BitmapSource);
            Photo.Thumbnail = _imageRetriever.ApplyRotateTransform(angle, Photo.Thumbnail);
            Photo.ReloadMetadata();
        }

        private void Favorite()
        {
            _mainViewModel.SelectedPhoto = Photo;
            _mainViewModel.Favorite();
        }

        private void MarkForDeletion()
        {
            _mainViewModel.SelectedPhoto = Photo;
            _mainViewModel.MarkForDeletion();
        }

        private void RenameToDate()
        {
            _mainViewModel.SelectedPhoto = Photo;
            _mainViewModel.RenameToDateAsync();
        }

        private void OpenPhotoInExplorer()
        {
            Photo.FilePath.OpenFileInExplorer();
        }

        private void WindowClosing()
        {
            _cancellationTokenSourceProvider.Cancel();
        }

        #endregion
    }
}