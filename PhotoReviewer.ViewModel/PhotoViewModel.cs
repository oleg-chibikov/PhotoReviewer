using System;
using System.Threading;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Common.Logging;
using GalaSoft.MvvmLight.Messaging;
using JetBrains.Annotations;
using PhotoReviewer.Core;
using PhotoReviewer.Resources;
using PhotoReviewer.View.Contracts;
using PropertyChanged;
using Scar.Common.Drawing;
using Scar.Common.Drawing.Data;
using Scar.Common.IO;
using Scar.Common.WPF.Commands;

namespace PhotoReviewer.ViewModel
{
    [ImplementPropertyChanged]
    public class PhotoViewModel : IDisposable
    {
        [NotNull]
        private readonly IExifTool exifTool;

        [NotNull]
        private readonly ILog logger;

        [NotNull]
        private readonly MainViewModel mainViewModel;

        [NotNull]
        private readonly IMessenger messenger;

        [NotNull]
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        public PhotoViewModel([NotNull] Photo photo, [NotNull] MainViewModel mainViewModel, [NotNull] WindowsArranger windowsArranger, [NotNull] ILog logger, [NotNull] IExifTool exifTool, [NotNull] IMessenger messenger)
        {
            if (photo == null)
                throw new ArgumentNullException(nameof(photo));
            if (mainViewModel == null)
                throw new ArgumentNullException(nameof(mainViewModel));
            if (windowsArranger == null)
                throw new ArgumentNullException(nameof(windowsArranger));
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            if (exifTool == null)
                throw new ArgumentNullException(nameof(exifTool));
            if (messenger == null)
                throw new ArgumentNullException(nameof(messenger));
            this.mainViewModel = mainViewModel;
            this.logger = logger;
            this.exifTool = exifTool;
            this.messenger = messenger;
            Photo = photo;
            ChangePhoto(photo);
            ToggleFullHeightCommand = new CorrelationCommand<IPhotoWindow>(windowsArranger.ToggleFullHeight);
            FavoriteCommand = new CorrelationCommand(Favorite);
            MarkForDeletionCommand = new CorrelationCommand(MarkForDeletion);
            RenameToDateCommand = new CorrelationCommand(RenameToDate);
            OpenPhotoInExplorerCommand = new CorrelationCommand(OpenPhotoInExplorer);
            ChangePhotoCommand = new CorrelationCommand<Photo>(ChangePhoto);
            RotateCommand = new CorrelationCommand<RotationType>(Rotate);
        }

        public void Dispose()
        {
            cancellationTokenSource.Dispose();
        }

        private CancellationToken RecreateCancellationToken()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;
            return token;
        }

        private void SelectAndAct([NotNull] Action action)
        {
            mainViewModel.SelectedPhoto = Photo;
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

        private void ChangePhoto([CanBeNull] Photo newPhoto)
        {
            if (newPhoto == null)
                return;
            //ViewedPhoto.Source = null;

            var token = RecreateCancellationToken();
            Photo = newPhoto;
            const int previewWidth = 800;
            var filePath = newPhoto.FilePath;
            var orientation = newPhoto.Metadata.Orientation;
            SelectAndAct(async () =>
            {
                try
                {
                    var bytes = await filePath.ReadAllFileAsync(token);
                    //Firstly load and display low quality image
                    BitmapSource = await bytes.LoadImageAsync(token, orientation, previewWidth);
                    //Then load full image
                    BitmapSource = await bytes.LoadImageAsync(token, orientation);
                }
                catch (OperationCanceledException)
                {
                }
            });
        }

        private async void Rotate(RotationType rotationType)
        {
            //TODO: Perform  physical rotation when no metadata
            logger.Info($"Rotating {Photo} {rotationType}...");
            //Pass photoCollection cancellation token instead of photo's one (need to cancel these operations only if switching collection path)
            Photo.Metadata.Orientation = Photo.Metadata.Orientation.GetNextOrientation(rotationType);
            Photo.SetThumbnailAsync(mainViewModel.PhotoCollection.CancellationTokenSource.Token);
            ChangePhoto(Photo);
            var errorText = await exifTool.SetOrientationAsync(Photo.Metadata.Orientation, Photo.FilePath, false, mainViewModel.PhotoCollection.CancellationTokenSource.Token);
            if (errorText != null)
            {
                messenger.Send(errorText, MessengerTokens.UserWarningToken);
                logger.Warn($"{Photo} is not rotated {rotationType}");
            }
            else
            {
                logger.Info($"{Photo} is rotated {rotationType}");
            }
        }

        private void Favorite()
        {
            SelectAndAct(() => mainViewModel.Favorite());
        }

        private void MarkForDeletion()
        {
            SelectAndAct(() => mainViewModel.MarkForDeletion());
        }

        private void RenameToDate()
        {
            SelectAndAct(() => mainViewModel.RenameToDate());
        }

        private void OpenPhotoInExplorer()
        {
            DirectoryUtility.OpenFileInExplorer(Photo.FilePath);
        }

        #endregion
    }
}