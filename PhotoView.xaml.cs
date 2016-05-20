using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace PhotoReviewer
{
    public partial class PhotoView
    {
        private static readonly DependencyProperty SelectedPhotoProperty = DependencyProperty<PhotoView>.Register(x => x.SelectedPhoto);
        private bool fullImageLoaded;
        private readonly Storyboard sb;

        public PhotoView(Photo selectedPhoto)
        {
            SelectedPhoto = selectedPhoto;
            InitializeComponent();
            ViewedPhoto.Source = SelectedPhoto.Image;
            var fade = new DoubleAnimation
            {
                From = 0.5,
                To = 1,
                Duration = TimeSpan.FromSeconds(0.3)
            };

            Storyboard.SetTarget(fade, ViewedPhoto);
            Storyboard.SetTargetProperty(fade, new PropertyPath(OpacityProperty));

            sb = new Storyboard();
            sb.Children.Add(fade);
        }

        public Photo SelectedPhoto
        {
            get { return (Photo)GetValue(SelectedPhotoProperty); }
            set { SetValue(SelectedPhotoProperty, value); }
        }

        #region Events

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Delete:
                case Key.Back:
                    SelectedPhoto.MarkForDeletion();
                    sb.Begin();
                    break;
                case Key.Enter:
                    SelectedPhoto.Favorite();
                    sb.Begin();
                    break;
                case Key.Left:
                    ChangePhoto(SelectedPhoto.Prev);
                    break;
                case Key.Right:
                    ChangePhoto(SelectedPhoto.Next);
                    break;
                case Key.Escape:
                    Close();
                    break;
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            switch (e.ChangedButton)
            {
                case MouseButton.XButton1:
                    ChangePhoto(SelectedPhoto.Prev);
                    break;
                case MouseButton.XButton2:
                    ChangePhoto(SelectedPhoto.Next);
                    break;
            }
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            ChangePhoto(SelectedPhoto.Prev);
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            ChangePhoto(SelectedPhoto.Next);
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            GC.Collect();
        }

        #endregion

        #region Private

        private void PhotoZoomBorder_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (fullImageLoaded)
                return;
            ViewedPhoto.Source = SelectedPhoto.FullImage;
            fullImageLoaded = true;
        }
        
        private void ChangePhoto(Photo photo)
        {
            if (photo == null)
                return;
            SelectedPhoto = photo;
            fullImageLoaded = false;
            ViewedPhoto.Source = SelectedPhoto.Image;
            var mainWindow = (MainWindow)Owner;
            mainWindow.PhotosListBox.SelectedIndex = photo.Index;
            mainWindow.PhotosListBox.ScrollIntoView(photo);
            PhotoZoomBorder.Reset();
            sb.Begin();
        }

        #endregion
    }
}