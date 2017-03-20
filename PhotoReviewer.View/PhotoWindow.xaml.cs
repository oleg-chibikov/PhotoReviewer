using System;
using System.ComponentModel;
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
            photoViewModel.PropertyChanged += PhotoViewModel_PropertyChanged;
            this.photoViewModel = photoViewModel;
            DataContext = photoViewModel;
            InitializeComponent();
            Show();
            FocusWindow();
        }

        protected override void BeforeClosing(object sender, CancelEventArgs e)
        {
            base.BeforeClosing(sender, e);
            photoViewModel.PropertyChanged -= PhotoViewModel_PropertyChanged;
        }

        //TODO: Xa
        private void PhotoViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(photoViewModel.Photo))
                ZoomBorder.Reset();
        }

        #region Private

        private void FocusWindow()
        {
            Activate();
            Topmost = true; // important
            Topmost = false; // important
            Focus(); // important
        }

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