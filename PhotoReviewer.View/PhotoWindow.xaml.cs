using System;
using System.Windows;
using JetBrains.Annotations;
using PhotoReviewer.Contracts.View;
using PhotoReviewer.Contracts.ViewModel;
using PhotoReviewer.ViewModel;

namespace PhotoReviewer.View
{
    [UsedImplicitly]
    internal sealed partial class PhotoWindow : IPhotoWindow
    {
        [NotNull] private readonly PhotoViewModel _photoViewModel;

        public PhotoWindow([NotNull] Window mainWindow, [NotNull] PhotoViewModel photoViewModel)
        {
            Owner = mainWindow;
            if (mainWindow == null)
                throw new ArgumentNullException(nameof(mainWindow));

            _photoViewModel = photoViewModel ?? throw new ArgumentNullException(nameof(photoViewModel));
            DataContext = photoViewModel;
            InitializeComponent();
            Show();
            Restore();
        }

        #region Private

        [NotNull]
        public IPhoto Photo => _photoViewModel.Photo;

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