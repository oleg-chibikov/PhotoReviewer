using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace PhotoReviewer
{
    public partial class PhotoView
    {
        public PhotoView()
        {
            InitializeComponent();
        }

        public Photo SelectedPhoto { private get; set; }

        public MainWindow MainWindow { private get; set; }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            OnLoad();
        }

        private void OnLoad()
        {
            var image = SelectedPhoto.Image;
            ViewedPhoto.Source = image;
            ViewedCaption.Content = SelectedPhoto.Name;
            Colorize();
        }

        private void ChangePhoto(Photo photo)
        {
            if (photo == null)
                return;
            SelectedPhoto = photo;
            MainWindow.PhotosListBox.SelectedIndex = photo.Index;
            MainWindow.PhotosListBox.ScrollIntoView(photo);
            OnLoad();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Delete:
                case Key.Back:
                    SelectedPhoto.MarkedForDeletion = !SelectedPhoto.MarkedForDeletion;
                    DbProvider.Save(SelectedPhoto.Source);
                    Colorize();
                    break;
                case Key.Left:
                    ChangePhoto(SelectedPhoto.Prev);
                    break;
                case Key.Right:
                    ChangePhoto(SelectedPhoto.Next);
                    break;
            }
        }

        private void Colorize()
        {
            PhotoBorder.Background = SelectedPhoto.MarkedForDeletion ? Brushes.Red : Brushes.White;
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
    }
}