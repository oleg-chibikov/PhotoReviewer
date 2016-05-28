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
using System.Windows.Media.Imaging;
using JetBrains.Annotations;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace PhotoReviewer
{
    public partial class PhotoView
    {
        [NotNull]
        private static readonly DependencyProperty SelectedPhotoProperty = DependencyProperty<PhotoView>.Register(x => x.SelectedPhoto);

        [NotNull]
        private readonly Storyboard fadeStoryBoard;

        [NotNull]
        private readonly MainWindow mainWindow;

        [NotNull]
        private readonly IList<PhotoView> photoViews;

        private bool fullImageLoaded;

        private bool isFullHeight;

        public PhotoView([NotNull] Photo selectedPhoto, [NotNull] IList<PhotoView> photoViews, [NotNull] MainWindow mainWindow)
        {
            InitializeComponent();
            this.mainWindow = mainWindow;
            PhotoZoomBorder.SetAction(LoadFullImage);
            this.photoViews = photoViews;
            var isFirst = !photoViews.Any();
            photoViews.Add(this);
            Show();
            ArrangeWindows(isFirst);
            SelectedPhoto = selectedPhoto;
            ViewedPhoto.Source = SelectedPhoto.Image;

            var fadeAnimation = new DoubleAnimation
            {
                From = 0.5,
                To = 1,
                Duration = TimeSpan.FromSeconds(0.3)
            };

            Storyboard.SetTarget(fadeAnimation, ViewedPhoto);
            Storyboard.SetTargetProperty(fadeAnimation, new PropertyPath(OpacityProperty));
            fadeStoryBoard = new Storyboard { Children = new TimelineCollection { fadeAnimation } };

            FocusWindow();
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
            MainWindow.OpenInExplorer(SelectedPhoto.FilePath);
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
                case Key.F:
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

        private void FullHeightButton_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
        {
            ToggleFullHeight();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!e.HeightChanged)
                return;
            var fullHeight = Screen.FromHandle(new WindowInteropHelper(mainWindow).Handle).WorkingArea.Height + WindowBorderWidth;
            var newHeight = e.NewSize.Height;
            var newSizeIsFullHeight = Math.Abs(newHeight - fullHeight) < 50;
            if (newSizeIsFullHeight != isFullHeight)
                ToggleFullHeightImage(this, newSizeIsFullHeight);
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
                        fadeStoryBoard.Begin();
                        onCompleted();
                    }, null);
                });
            });
            fullImageLoaded = true;
            return true;
        }

        private void SelectAndAct([NotNull] Action action)
        {
            mainWindow.PhotosListBox.SelectedItem = SelectedPhoto;
            mainWindow.ScrollToSelected();
            action();
            fadeStoryBoard.Begin();
        }

        private void ChangePhoto([CanBeNull] Photo photo)
        {
            if (photo == null)
                return;
            fullImageLoaded = false;
            SelectedPhoto = photo;
            ViewedPhoto.Source = SelectedPhoto.Image;
            SelectAndAct(() => PhotoZoomBorder.Reset());
        }

        private const double WindowBorderWidth = 8;

        private void ArrangeWindows(bool defaultHeight = false)
        {
            var scr = Screen.FromHandle(new WindowInteropHelper(mainWindow).Handle);
            if (photoViews.Any())
            {
                var thirdHeight = scr.WorkingArea.Height / 3;
                mainWindow.WindowState = WindowState.Normal;
                mainWindow.Top = scr.WorkingArea.Top;
                mainWindow.Height = thirdHeight;
                mainWindow.Width = scr.WorkingArea.Width + 2 * WindowBorderWidth;
                mainWindow.Left = scr.WorkingArea.Left - WindowBorderWidth;
                mainWindow.ScrollToSelected();
                double left = scr.WorkingArea.Left;
                var width = scr.WorkingArea.Width / photoViews.Count;
                var twoThirdsHeight = thirdHeight * 2;
                double firstTop, firstHeight;
                bool isFirstFullHeight;
                if (defaultHeight)
                {
                    firstTop = scr.WorkingArea.Top + thirdHeight - WindowBorderWidth;
                    firstHeight = twoThirdsHeight + 2 * WindowBorderWidth;
                    isFirstFullHeight = false;
                }
                else
                {
                    var first = photoViews.First();
                    firstTop = first.Top;
                    firstHeight = first.Height;
                    isFirstFullHeight = first.isFullHeight;
                }
                var actualWidth = width + 2 * WindowBorderWidth;
                foreach (var photoView in photoViews)
                {
                    photoView.WindowStartupLocation = WindowStartupLocation.Manual;
                    photoView.WindowState = WindowState.Normal;
                    photoView.Left = left - WindowBorderWidth;
                    left += width;
                    photoView.Width = actualWidth;
                    photoView.Top = firstTop;
                    photoView.Height = firstHeight;
                    ToggleFullHeightImage(photoView, isFirstFullHeight);
                }
                Task.Run(() =>
                {
                    //Wait while main window is scrolling to selected item
                    Thread.Sleep(500);
                    if (photoViews.Any())
                        Dispatcher.Invoke(photoViews.Last().FocusWindow);
                });
            }
            else
                mainWindow.WindowState = WindowState.Maximized;
        }

        private static readonly BitmapSource FullScreenImg = new BitmapImage(new Uri(@"/img/FullScreen.png", UriKind.Relative));
        private static readonly BitmapSource FullScreenExitImg = new BitmapImage(new Uri(@"/img/FullScreenExit.png", UriKind.Relative));

        private void ToggleFullHeight()
        {
            var scr = Screen.FromHandle(new WindowInteropHelper(mainWindow).Handle);
            var fullHeight = scr.WorkingArea.Height + WindowBorderWidth;
            if (isFullHeight)
                ArrangeWindows(true);
            else
                foreach (var photoView in photoViews)
                {
                    photoView.Top = 0;
                    photoView.Height = fullHeight;
                    ToggleFullHeightImage(photoView, true);
                }
        }

        private static void ToggleFullHeightImage(PhotoView photoView, bool isFullheight)
        {
            photoView.FullHeightImage.Source = isFullheight ? FullScreenExitImg : FullScreenImg;
            photoView.isFullHeight = isFullheight;
        }

        private void Favorite()
        {
            SelectAndAct(() => mainWindow.Favorite());
        }

        private void MarkForDeletion()
        {
            SelectAndAct(() => mainWindow.MarkAsDeleted());
        }

        private void RenameToDate()
        {
            SelectAndAct(() => mainWindow.RenameToDate());
        }

        private void FocusWindow()
        {
            Activate();
            Topmost = true; // important
            Topmost = false; // important
            Focus(); // important
        }

        #endregion
    }
}