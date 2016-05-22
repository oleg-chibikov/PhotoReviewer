using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using JetBrains.Annotations;
using Application = System.Windows.Application;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace PhotoReviewer
{
    public partial class PhotoView
    {
        [NotNull]
        private static readonly DependencyProperty SelectedPhotoProperty = DependencyProperty<PhotoView>.Register(x => x.SelectedPhoto);

        [NotNull]
        private readonly Storyboard sb;

        [NotNull]
        private readonly IList<PhotoView> photoViews;

        private bool fullImageLoaded;

        public PhotoView([NotNull] Photo selectedPhoto, [NotNull] IList<PhotoView> photoViews)
        {
            InitializeComponent();
            PhotoZoomBorder.SetAction(LoadFullImage);
            this.photoViews = photoViews;
            photoViews.Add(this);
            Show();
            ArrangeWindows();
            SelectedPhoto = selectedPhoto;
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

        [NotNull]
        public Photo SelectedPhoto
        {
            get { return (Photo)GetValue(SelectedPhotoProperty); }
            set { SetValue(SelectedPhotoProperty, value); }
        }

        #region Events
        
        private void MarkAsDeletedMenuItem_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
        {
            MarkForDeletion();
        }

        private void FavoriteMenuItem_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
        {
            Favorite();
        }

        private void OpenInExplorerMenuItem_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
        {
            MainWindow.OpenInExplorer(SelectedPhoto.Path);
        }

        private void RenameToDateMenuItem_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
        {
            RenameToDate();
        }

        private void Window_KeyDown([NotNull] object sender, [NotNull] KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Delete:
                case Key.Back:
                    MarkForDeletion();
                    break;
                case Key.Enter:
                    Favorite();
                    break;
                case Key.Left:
                    ChangePhoto(SelectedPhoto.Prev);
                    break;
                case Key.Right:
                    ChangePhoto(SelectedPhoto.Next);
                    break;
                case Key.R:
                    if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                        RenameToDate();
                    break;
                case Key.Escape:
                    Close();
                    break;
            }
        }

        private void Window_MouseDown([NotNull] object sender, [NotNull] MouseButtonEventArgs e)
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

        private void PrevButton_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
        {
            ChangePhoto(SelectedPhoto.Prev);
        }

        private void NextButton_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
        {
            ChangePhoto(SelectedPhoto.Next);
        }

        private void Window_Closed([NotNull] object sender, [NotNull] EventArgs e)
        {
            photoViews.Remove(this);
            ArrangeWindows();
            GC.Collect();
        }
        
        #endregion

        #region Private

        private bool LoadFullImage([NotNull] Action onCompleted)
        {
            if (fullImageLoaded)
                return false;
            ViewedPhoto.Source = SelectedPhoto.GetFullImage((x, xx) =>
            {
                var context = SynchronizationContext.Current;
                Task.Run(() =>
                {
                    Thread.Sleep(100); //Wait while image is displayed
                    context.Send(t =>
                    {
                        sb.Begin();
                        onCompleted();
                    }, null);
                });
            });
            fullImageLoaded = true;
            return true;
        }

        private void ChangePhoto([CanBeNull] Photo photo)
        {
            if (photo == null)
                return;
            SelectedPhoto = photo;
            fullImageLoaded = false;
            ViewedPhoto.Source = SelectedPhoto.Image;
            var mainWindow = (MainWindow)Owner;
            mainWindow.PhotosListBox.SelectedIndex = photo.Index;
            mainWindow.ScrollToSelected();
            PhotoZoomBorder.Reset();
            sb.Begin();
        }

        private void ArrangeWindows()
        {
            const double borderWidth = 8;
            var mainWindow = (MainWindow)Application.Current.MainWindow;
            var scr = Screen.FromHandle(new WindowInteropHelper(mainWindow).Handle);
            if (photoViews.Any())
            {
                var width = scr.WorkingArea.Width / photoViews.Count;
                double left = scr.WorkingArea.Left;
                var thirdHeight = scr.WorkingArea.Height / 3;
                var twoThirdsHeight = thirdHeight * 2;
                foreach (var photoView in photoViews)
                {
                    photoView.WindowStartupLocation = WindowStartupLocation.Manual;
                    photoView.WindowState = WindowState.Normal;
                    photoView.Left = left - borderWidth;
                    left += width;
                    photoView.Width = width + 2 * borderWidth;
                    photoView.Top = scr.WorkingArea.Y + thirdHeight - borderWidth;
                    photoView.Height = twoThirdsHeight + 2 * borderWidth;
                }
                mainWindow.WindowState = WindowState.Normal;
                mainWindow.Top = scr.WorkingArea.Y;
                mainWindow.Height = thirdHeight;
                mainWindow.Width = scr.WorkingArea.Width + 2 * borderWidth;
                mainWindow.Left = scr.WorkingArea.X - borderWidth;
            }
            else
                mainWindow.WindowState = WindowState.Maximized;
            mainWindow.ScrollToSelected();
        }

        private void Favorite()
        {
            SelectedPhoto.Favorite();
            sb.Begin();
        }

        private void MarkForDeletion()
        {
            SelectedPhoto.MarkForDeletion();
            sb.Begin();
        }

        private void RenameToDate()
        {
            var newPath = SelectedPhoto.RenameToDate();
            if (newPath != null)
                ((MainWindow)Owner).ChangeSelectedItemWithWait(newPath);
        }

        #endregion
    }
}