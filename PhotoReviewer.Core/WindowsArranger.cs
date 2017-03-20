using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using JetBrains.Annotations;
using PhotoReviewer.View.Contracts;
using Scar.Common.WPF;

namespace PhotoReviewer.Core
{
    [UsedImplicitly]
    public class WindowsArranger
    {
        private const double WindowBorderWidth = 8;

        [NotNull]
        private IList<IPhotoWindow> PhotoWindows { get; } = new List<IPhotoWindow>();

        private readonly WindowFactory<IMainWindow> mainWindowFactory;

        public WindowsArranger([NotNull] WindowFactory<IMainWindow> mainWindow, [NotNull] WindowFactory<IMainWindow> mainWindowFactory)
        {
            if (mainWindow == null)
                throw new ArgumentNullException(nameof(mainWindow));
            if (mainWindowFactory == null)
                throw new ArgumentNullException(nameof(mainWindowFactory));
            this.mainWindowFactory = mainWindowFactory;
        }

        private void ArrangeWindows(bool defaultHeight = false)
        {
            var mainWindow = mainWindowFactory.GetWindow();
            //TODO: lock (only one arrangement at a time)
            if (PhotoWindows.Any())
            {
                var activeScreenArea = mainWindow.ActiveScreenArea;
                var thirdHeight = activeScreenArea.Height / 3;
                mainWindow.WindowState = WindowState.Normal;
                mainWindow.Top = activeScreenArea.Top;
                mainWindow.Height = thirdHeight;
                mainWindow.Width = activeScreenArea.Width + 2 * WindowBorderWidth;
                mainWindow.Left = activeScreenArea.Left - WindowBorderWidth;
                //TODO:
                //mainWindow.ScrollToSelected();
                double left = activeScreenArea.Left;
                var width = activeScreenArea.Width / PhotoWindows.Count;
                var twoThirdsHeight = thirdHeight * 2;
                double firstTop, firstHeight;
                if (defaultHeight)
                {
                    firstTop = activeScreenArea.Top + thirdHeight - WindowBorderWidth;
                    firstHeight = twoThirdsHeight + 2 * WindowBorderWidth;
                }
                else
                {
                    var first = PhotoWindows.First();
                    firstTop = first.Top;
                    firstHeight = first.Height;
                }
                var actualWidth = width + 2 * WindowBorderWidth;
                foreach (var photoView in PhotoWindows)
                {
                    photoView.WindowState = WindowState.Normal;
                    photoView.Left = left - WindowBorderWidth;
                    left += width;
                    photoView.Width = actualWidth;
                    photoView.Top = firstTop;
                    photoView.Height = firstHeight;
                }
                //TODO: Focus main window if all windows are closed
                //Task.Run(() =>
                //{
                //    //Wait while main window is scrolling to selected item
                //    Thread.Sleep(500);
                //    if (mainViewModel.PhotoWindows.Any())
                //        Dispatcher.Invoke(mainViewModel.PhotoWindows.Last().FocusWindow);
                //});
            }
            else
            {
                mainWindow.WindowState = WindowState.Maximized;
            }
        }

        public void ToggleFullHeight([NotNull] IPhotoWindow currentWindow)
        {
            if (currentWindow == null)
                throw new ArgumentNullException(nameof(currentWindow));
            if (PhotoWindows.Count > 1)
            {
                var activeScreenArea = currentWindow.ActiveScreenArea;
                var fullHeight = activeScreenArea.Height + WindowBorderWidth;
                if (currentWindow.IsFullHeight)
                    ArrangeWindows(true);
                else
                    foreach (var photoView in PhotoWindows)
                    {
                        photoView.Top = 0;
                        photoView.Height = fullHeight;
                    }
            }
            else
                ToggleFullScreen(currentWindow);
        }

        public void Add(IPhotoWindow window)
        {
            window.Closed += Window_Closed;
            var isFirst = !PhotoWindows.Any();
            PhotoWindows.Add(window);
            ArrangeWindows(isFirst);
        }

        //TODO: SizeChanged in window + debounce

        private void Window_Closed(object sender, EventArgs e)
        {
            var window = (IPhotoWindow)sender;
            window.Closed -= Window_Closed;

            PhotoWindows.Remove(window);
            if (PhotoWindows.Count == 1 && PhotoWindows.Single().IsFullHeight)
                ToggleFullScreen(PhotoWindows.Single());
            else
                ArrangeWindows();
            GC.Collect();
        }

        private void ToggleFullScreen(IWindow window)
        {
            if (window.WindowState != WindowState.Maximized)
            {
                window.WindowState = WindowState.Maximized;
                // hide the window before changing window style
                window.Visibility = Visibility.Collapsed;
                window.Topmost = true;
                window.WindowStyle = WindowStyle.None;
                window.ResizeMode = ResizeMode.NoResize;
                // re-show the window after changing style
                window.Visibility = Visibility.Visible;
            }
            else
            {
                window.WindowState = WindowState.Normal;
                window.Topmost = false;
                window.WindowStyle = WindowStyle.SingleBorderWindow;
                window.ResizeMode = ResizeMode.CanResize;
                ArrangeWindows(true);
            }
        }

        //private static void ToggleFullHeightImage(PhotoViewModelWithWindow window, bool isFullheight)
        //{
        //    window.ViewModel.FullHeightIcon = isFullheight ? FullScreenExitIcon : FullScreenIcon;
        //    window.ViewModel.IsFullHeight = isFullheight;
        //}

        public void ClosePhotos([CanBeNull] string path = null)
        {
            for (var i = 0; i < PhotoWindows.Count; i++)
            {
                var photoViewModel = PhotoWindows[i];
                if (path == null || photoViewModel.PhotoPath == path)
                {
                    photoViewModel.Close();
                    i--;
                }
            }
        }
    }
}