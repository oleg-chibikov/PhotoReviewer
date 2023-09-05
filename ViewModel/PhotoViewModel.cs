using System;
using System.Threading;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Easy.MessageHub;
using Microsoft.Extensions.Logging;
using PhotoReviewer.Contracts.View;
using PhotoReviewer.Core;
using PhotoReviewer.Resources;
using PropertyChanged;
using Scar.Common.Async;
using Scar.Common.ImageProcessing.ExifExtraction;
using Scar.Common.ImageProcessing.Metadata;
using Scar.Common.IO;
using Scar.Common.Messages;
using Scar.Common.MVVM.Commands;
using Scar.Common.WPF.ImageRetrieval;

namespace PhotoReviewer.ViewModel
{
    [AddINotifyPropertyChangedInterface]

    public partial class PhotoViewModel
    {
        readonly ICancellationTokenSourceProvider _cancellationTokenSourceProvider;

        readonly IExifTool _exifTool;

        readonly IImageRetriever _imageRetriever;

        readonly ILogger _logger;

        readonly MainViewModel _mainViewModel;

        readonly IMessageHub _messenger;

        public PhotoViewModel(
            Photo photo,
            MainViewModel mainViewModel,
            WindowsArranger windowsArranger,
            ILogger<PhotoViewModel> logger,
            IExifTool exifTool,
            IMessageHub messenger,
            ICancellationTokenSourceProvider cancellationTokenSourceProvider,
            IImageRetriever imageRetriever,
            ICommandManager commandManager)
        {
            _imageRetriever = imageRetriever ?? throw new ArgumentNullException(nameof(imageRetriever));
            _cancellationTokenSourceProvider = cancellationTokenSourceProvider ?? throw new ArgumentNullException(nameof(cancellationTokenSourceProvider));
            _mainViewModel = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _exifTool = exifTool ?? throw new ArgumentNullException(nameof(exifTool));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            _ = commandManager ?? throw new ArgumentNullException(nameof(commandManager));
            Photo = photo ?? throw new ArgumentNullException(nameof(photo));
            windowsArranger = windowsArranger ?? throw new ArgumentNullException(nameof(windowsArranger));

            ChangePhotoAsync(ChangeType.Reload);
            ToggleFullHeightCommand = new CorrelationCommand<IPhotoWindow>(commandManager, windowsArranger.ToggleFullHeight);
            FavoriteCommand = new CorrelationCommand(commandManager, Favorite);
            MarkForDeletionCommand = new CorrelationCommand(commandManager, MarkForDeletion);
            RenameToDateCommand = new CorrelationCommand(commandManager, RenameToDate);
            OpenPhotoInExplorerCommand = new CorrelationCommand(commandManager, OpenPhotoInExplorer);
            ChangePhotoCommand = new CorrelationCommand<ChangeType>(commandManager, ChangePhotoAsync);
            RotateCommand = new CorrelationCommand<RotationType>(commandManager, RotateAsync);
            WindowClosingCommand = new CorrelationCommand(commandManager, WindowClosing);
        }

        [DependsOn(nameof(Photo))]
        public bool NextPhotoAvailable => Photo.Next != null;

        [DependsOn(nameof(Photo))]
        public bool PrevPhotoAvailable => Photo.Prev != null;

        public ICommand ToggleFullHeightCommand { get; }

        public ICommand FavoriteCommand { get; }

        public ICommand MarkForDeletionCommand { get; }

        public ICommand OpenPhotoInExplorerCommand { get; }

        public ICommand RenameToDateCommand { get; }

        public ICommand ChangePhotoCommand { get; }

        public ICommand RotateCommand { get; }

        public ICommand WindowClosingCommand { get; }

        public BitmapSource? BitmapSource { get; set; }

        public Photo Photo { get; set; }

        async void ChangePhotoAsync(ChangeType changeType)
        {
            Photo? newPhoto;
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
            {
                return;
            }

            await _cancellationTokenSourceProvider.StartNewTaskAsync(
                    async token =>
                    {
                        _mainViewModel.SelectedPhoto = Photo;
                        newPhoto.ReloadCollectionInfoIfNeeded();
                        Photo = newPhoto;
                        const int previewWidth = 800;
                        var newPhotoFileLocation = newPhoto.FileLocation;
                        var orientation = newPhoto.Metadata.Orientation;
                        try
                        {
                            var bytes = await newPhotoFileLocation.ToString().ReadFileAsync(token).ConfigureAwait(false);

                            // Firstly load and display low quality image
                            BitmapSource = await _imageRetriever.LoadImageAsync(bytes, token, orientation, previewWidth).ConfigureAwait(false);

                            // Then load full image
                            BitmapSource = await _imageRetriever.LoadImageAsync(bytes, token, orientation).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Cannot load image {FilePath}", newPhotoFileLocation);
                            _messenger.Publish($"Cannot load image {newPhotoFileLocation}".ToError());
                        }
                    })
                .ConfigureAwait(true);
        }

        // TODO: Move to PhotoCollection. Apply multiple rotations at once
        async void RotateAsync(RotationType rotationType)
        {
            _logger.LogInformation("Rotating {Photo} {RotationType}...", Photo, rotationType);

            if (!Photo.OrientationIsSpecified)
            {
                _logger.LogWarning("Orientation is not specified for {Photo}", Photo);
                _messenger.Publish(Errors.NoMetadata.ToWarning());
                return;
            }

            var originalOrientation = Photo.Metadata.Orientation;
            Photo.Metadata.Orientation = originalOrientation.GetNextOrientation(rotationType);
            var angle = rotationType == RotationType.Clockwise ? 90 : -90;

            await _cancellationTokenSourceProvider.StartNewTaskAsync(SetOrientationAsync).ConfigureAwait(true);
            return;

            async void SetOrientationAsync(CancellationToken token)
            {
                try
                {
                    // no need to cancel this operation (if rotation starts it should be finished)
                    var task = _exifTool.SetOrientationAsync(Photo.Metadata.Orientation, Photo.FileLocation.ToString(), false, token);
                    RotateVisualRepresentation(angle);
                    await task.ConfigureAwait(true);
                    _logger.LogInformation("{Photo} is rotated {RotationType}", Photo, rotationType);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Rotation is cancelled");
                }
                catch (InvalidOperationException ex)
                {
                    _messenger.Publish(Errors.RotationFailed.ToWarning());
                    _logger.LogWarning(ex, "Rotation failed");
                    Photo.Metadata.Orientation = originalOrientation;
                    RotateVisualRepresentation(-angle);
                }
            }
        }

        void RotateVisualRepresentation(int angle)
        {
            BitmapSource = _imageRetriever.ApplyRotateTransform(angle, BitmapSource ?? throw new InvalidOperationException());
            Photo.Thumbnail = _imageRetriever.ApplyRotateTransform(angle, Photo.Thumbnail ?? throw new InvalidOperationException());
            Photo.ReloadMetadata();
        }

        void Favorite()
        {
            _mainViewModel.SelectedPhoto = Photo;
            _mainViewModel.Favorite();
        }

        void MarkForDeletion()
        {
            _mainViewModel.SelectedPhoto = Photo;
            _mainViewModel.MarkForDeletion();
        }

        void RenameToDate()
        {
            _mainViewModel.SelectedPhoto = Photo;
            _mainViewModel.RenameToDateAsync();
        }

        void OpenPhotoInExplorer()
        {
            Photo.FileLocation.ToString().OpenFileInExplorer();
        }

        void WindowClosing()
        {
            _cancellationTokenSourceProvider.Cancel();
        }
    }
}
