using System;
using System.Windows;
using JetBrains.Annotations;
using PhotoReviewer.View.Contracts;
using PhotoReviewer.ViewModel;

namespace PhotoReviewer.View
{
    [UsedImplicitly]
    public partial class PhotoWindow : IPhotoWindow
    {
        [NotNull]
        private readonly PhotoViewModel photoViewModel;

        public PhotoWindow([NotNull] Window mainWindow, [NotNull] PhotoViewModel photoViewModel)
        {
            Owner = mainWindow;
            if (mainWindow == null)
                throw new ArgumentNullException(nameof(mainWindow));
            if (photoViewModel == null)
                throw new ArgumentNullException(nameof(photoViewModel));
            this.photoViewModel = photoViewModel;
            DataContext = photoViewModel;
            InitializeComponent();
            Show();
            Restore();
        }

        #region Private

        public string PhotoPath => photoViewModel.Photo.FilePath;

        #endregion

        #region Events

        //TODO: implement
        //private void Window_MouseDown([NotNull] object sender, [NotNull] MouseButtonEventArgs e)
        //{
        //    switch (e.ChangedButton)
        //    {
        //        case MouseButton.XButton1:
        //            ChangePhoto(SelectedPhoto.Prev);
        //            break;
        //        case MouseButton.XButton2:
        //            ChangePhoto(SelectedPhoto.Next);
        //            break;
        //    }
        //}

        #endregion
    }
}