using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Common.Logging;
using JetBrains.Annotations;
using PhotoReviewer.View.Contracts;
using Scar.Common.WPF.View;
using Scar.Common.WPF.View.Contracts;

namespace PhotoReviewer.Core
{
    [UsedImplicitly]
    public class WindowsArranger
    {
        private const double WindowBorderWidth = 8;

        [NotNull]
        private readonly ILog logger;

        [NotNull]
        private readonly WindowFactory<IMainWindow> mainWindowFactory;

        public WindowsArranger([NotNull] WindowFactory<IMainWindow> mainWindowFactory, [NotNull] ILog logger)
        {
            if (mainWindowFactory == null)
                throw new ArgumentNullException(nameof(mainWindowFactory));
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            this.mainWindowFactory = mainWindowFactory;
            this.logger = logger;
        }

        [NotNull]
        private IList<IPhotoWindow> PhotoWindows { get; } = new List<IPhotoWindow>();

        private void ArrangeWindows(bool defaultHeight = false)
        {
            logger.Debug("Arranging windows...");
            var mainWindow = mainWindowFactory.GetWindow();
            //TODO: lock (only one arrangement at a time)
            if (PhotoWindows.Any())
            {
                var activeScreenArea = mainWindow.ActiveScreenArea;
                double left = activeScreenArea.Left;
                var width = activeScreenArea.Width / PhotoWindows.Count;
                double firstPhotoTop, firstPhotoHeight, mainWindowHeight;

                if (defaultHeight)
                {
                    var thirdHeight = activeScreenArea.Height / 3;
                    var twoThirdsHeight = thirdHeight * 2;
                    firstPhotoTop = activeScreenArea.Top + thirdHeight - WindowBorderWidth;
                    firstPhotoHeight = twoThirdsHeight + 2 * WindowBorderWidth;
                    mainWindowHeight = thirdHeight;
                }
                else
                {
                    var firstPhoto = PhotoWindows.First();
                    firstPhotoTop = firstPhoto.Top;
                    firstPhotoHeight = firstPhoto.Height;
                    mainWindowHeight = activeScreenArea.Height - firstPhotoHeight + 2 * WindowBorderWidth;
                }
                logger.Debug($"Arranging as {mainWindowHeight:##.#}:{firstPhotoHeight:##.#}...");

                mainWindow.WindowState = WindowState.Normal;
                mainWindow.Top = activeScreenArea.Top;
                mainWindow.Width = activeScreenArea.Width + 2 * WindowBorderWidth;
                mainWindow.Left = activeScreenArea.Left - WindowBorderWidth;
                mainWindow.Height = mainWindowHeight;

                var actualWidth = width + 2 * WindowBorderWidth;
                foreach (var photoWindow in PhotoWindows)
                {
                    photoWindow.WindowState = WindowState.Normal;
                    photoWindow.Left = left - WindowBorderWidth;
                    left += width;
                    photoWindow.Width = actualWidth;
                    photoWindow.Top = firstPhotoTop;
                    photoWindow.Height = firstPhotoHeight;
                }

                if (PhotoWindows.Any())
                    PhotoWindows.Last().Restore();
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
            {
                ToggleFullScreen(currentWindow);
            }
        }

        public void Add([NotNull] IPhotoWindow window)
        {
            if (window == null)
                throw new ArgumentNullException(nameof(window));
            window.Closed += Window_Closed;
            var isFirst = !PhotoWindows.Any();
            PhotoWindows.Add(window);
            ArrangeWindows(isFirst);
        }

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
            if (window.IsFullHeight)
            {
                logger.Debug("Normalizing window view...");
                window.WindowState = WindowState.Normal;
                window.Topmost = false;
                window.WindowStyle = WindowStyle.SingleBorderWindow;
                window.ResizeMode = ResizeMode.CanResize;
                ArrangeWindows(true);
            }
            else
            {
                logger.Debug("Maximizing window...");
                window.WindowState = WindowState.Maximized;
                // hide the window before changing window style
                window.Visibility = Visibility.Collapsed;
                window.Topmost = true;
                window.WindowStyle = WindowStyle.None;
                window.ResizeMode = ResizeMode.NoResize;
                // re-show the window after changing style
                window.Visibility = Visibility.Visible;
            }
        }

        public void ClosePhotos([CanBeNull] string filePath = null)
        {
            if (filePath == null)
                logger.Debug("Closing all views...");
            for (var i = 0; i < PhotoWindows.Count; i++)
            {
                var photoViewModel = PhotoWindows[i];
                if (filePath == null || photoViewModel.PhotoPath == filePath)
                {
                    logger.Debug($"Closing view for {photoViewModel.PhotoPath}...");
                    photoViewModel.Close();
                    i--;
                }
            }
        }
    }
}