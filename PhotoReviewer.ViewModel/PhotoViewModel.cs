using System;
using System.Threading;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using JetBrains.Annotations;
using PhotoReviewer.Core;
using PhotoReviewer.View.Contracts;
using Scar.Common.Drawing;
using Scar.Common.IO;
using Scar.Common.WPF;

namespace PhotoReviewer.ViewModel
{
    public class PhotoViewModel : ViewModelBase, IRequestCloseViewModel, IDisposable
    {
        [NotNull]
        private readonly MainViewModel mainViewModel;

        [NotNull]
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        public PhotoViewModel([NotNull] IMessenger messenger, [NotNull] Photo photo, [NotNull] MainViewModel mainViewModel, [NotNull] WindowsArranger windowsArranger) : base(messenger)
        {
            if (messenger == null)
                throw new ArgumentNullException(nameof(messenger));
            if (photo == null)
                throw new ArgumentNullException(nameof(photo));
            if (mainViewModel == null)
                throw new ArgumentNullException(nameof(mainViewModel));
            if (windowsArranger == null)
                throw new ArgumentNullException(nameof(windowsArranger));
            this.mainViewModel = mainViewModel;
            Photo = photo;
            ChangePhoto(photo);
            ToggleFullHeightCommand = new RelayCommand<IPhotoWindow>(windowsArranger.ToggleFullHeight);
            FavoriteCommand = new RelayCommand(Favorite);
            MarkForDeletionCommand = new RelayCommand(MarkForDeletion);
            RenameToDateCommand = new RelayCommand(RenameToDate);
            OpenPhotoInExplorerCommand = new RelayCommand(OpenPhotoInExplorer);
            CloseCommand = new RelayCommand(Close);
            ChangePhotoCommand = new RelayCommand<Photo>(ChangePhoto);
        }

        public event EventHandler RequestClose;

        private CancellationToken RecreateCancellationToken()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;
            return token;
        }

        private void ChangePhoto([CanBeNull] Photo newPhoto)
        {
            if (newPhoto == null)
                return;
            //ViewedPhoto.Source = null;

            var token = RecreateCancellationToken();
            Photo = newPhoto;
            const int width = 200;
            var filePath = newPhoto.FilePath;
            var orientation = newPhoto.Metadata.Orientation;
            SelectAndAct(async () =>
            {
                try
                {
                    var bytes = await filePath.ReadAllFileAsync(token);
                    //Firstly load and display low quality image
                    BitmapSource = await bytes.LoadImageAsync(token, orientation, width);
                    //Then load full image
                    BitmapSource = await bytes.LoadImageAsync(token, orientation);
                }
                catch (OperationCanceledException)
                {
                }
            });
        }

        private void SelectAndAct([NotNull] Action action)
        {
            mainViewModel.SelectedPhoto = Photo;
            action();
        }

        #region Dependency Properties

        private BitmapSource bitmapSource;

        public BitmapSource BitmapSource
        {
            get { return bitmapSource; }
            set { Set(() => BitmapSource, ref bitmapSource, value); }
        }

        private Photo photo;

        [NotNull]
        public Photo Photo
        {
            get { return photo; }
            set { Set(() => Photo, ref photo, value); }
        }

        #endregion

        #region Commands

        public ICommand ToggleFullHeightCommand { get; }
        public ICommand FavoriteCommand { get; }
        public ICommand MarkForDeletionCommand { get; }
        public ICommand OpenPhotoInExplorerCommand { get; }
        public ICommand RenameToDateCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand ChangePhotoCommand { get; }

        #endregion

        #region Command Handlers

        public void Close()
        {
            RequestClose?.Invoke(null, null);
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

        public void Dispose()
        {
            cancellationTokenSource.Dispose();
        }

        #endregion
    }
}