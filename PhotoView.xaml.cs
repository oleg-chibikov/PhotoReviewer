using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace PhotoReviewer
{
    public partial class PhotoView
    {
        public PhotoView(Photo selectedPhoto)
        {
            SelectedPhoto = selectedPhoto;
            InitializeComponent();
        }

        private bool fullImageLoaded;

        private static readonly DependencyProperty SelectedPhotoProperty = DependencyProperty<PhotoView>.Register(x => x.SelectedPhoto);

        public Photo SelectedPhoto
        {
            get { return (Photo)GetValue(SelectedPhotoProperty); }
            set { SetValue(SelectedPhotoProperty, value); }
        }

        private void ChangePhoto(Photo photo)
        {
            if (photo == null)
                return;
            SelectedPhoto = photo;
            if (fullImageLoaded)
            {
                var bind = new Binding
                {
                    Path = new PropertyPath("SelectedPhoto.Image")
                };
                ViewedPhoto.SetBinding(Image.SourceProperty, bind);
                fullImageLoaded = false;
            }
            var mainWindow = (MainWindow)Owner;
            mainWindow.PhotosListBox.SelectedIndex = photo.Index;
            mainWindow.PhotosListBox.ScrollIntoView(photo);
            PhotoZoomBorder.Reset();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Delete:
                case Key.Back:
                    SelectedPhoto.MarkedForDeletion = !SelectedPhoto.MarkedForDeletion;
                    DbProvider.Save(SelectedPhoto.Source, DbProvider.OperationType.MarkForDeletion);
                    break;
                case Key.Enter:
                    SelectedPhoto.Favorited = !SelectedPhoto.Favorited;
                    DbProvider.Save(SelectedPhoto.Source, DbProvider.OperationType.Favorite);
                    break;
                case Key.Left:
                    ChangePhoto(SelectedPhoto.Prev);
                    break;
                case Key.Right:
                    ChangePhoto(SelectedPhoto.Next);
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

        private void PhotoZoomBorder_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (fullImageLoaded)
                return;
            ViewedPhoto.Source = SelectedPhoto.FullImage;
            fullImageLoaded = true;
        }
    }
}