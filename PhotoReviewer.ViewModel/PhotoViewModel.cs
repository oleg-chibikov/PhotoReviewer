using System;
using System.Threading;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using JetBrains.Annotations;
using PhotoReviewer.Core;
using PhotoReviewer.View.Contracts;
using PropertyChanged;
using Scar.Common.Drawing;
using Scar.Common.IO;
using Scar.Common.WPF.Commands;

namespace PhotoReviewer.ViewModel
{
    [ImplementPropertyChanged]
    public class PhotoViewModel : IDisposable
    {
        [NotNull]
        private readonly MainViewModel mainViewModel;

        [NotNull]
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        public PhotoViewModel([NotNull] Photo photo, [NotNull] MainViewModel mainViewModel, [NotNull] WindowsArranger windowsArranger)
        {
            if (photo == null)
                throw new ArgumentNullException(nameof(photo));
            if (mainViewModel == null)
                throw new ArgumentNullException(nameof(mainViewModel));
            if (windowsArranger == null)
                throw new ArgumentNullException(nameof(windowsArranger));
            this.mainViewModel = mainViewModel;
            Photo = photo;
            ChangePhoto(photo);
            ToggleFullHeightCommand = new CorrelationCommand<IPhotoWindow>(windowsArranger.ToggleFullHeight);
            FavoriteCommand = new CorrelationCommand(Favorite);
            MarkForDeletionCommand = new CorrelationCommand(MarkForDeletion);
            RenameToDateCommand = new CorrelationCommand(RenameToDate);
            OpenPhotoInExplorerCommand = new CorrelationCommand(OpenPhotoInExplorer);
            ChangePhotoCommand = new CorrelationCommand<Photo>(ChangePhoto);
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