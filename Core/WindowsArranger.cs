using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using Microsoft.Extensions.Logging;
using PhotoReviewer.Contracts.View;
using PhotoReviewer.Contracts.ViewModel;
using Scar.Common.View.WindowCreation;

namespace PhotoReviewer.Core
{
    public sealed class WindowsArranger
    {
        const double WindowBorderWidth = 8;

        readonly object _lockObject = new ();

        readonly ILogger _logger;

        readonly WindowFactory<IMainWindow> _mainWindowFactory;

        public WindowsArranger(WindowFactory<IMainWindow> mainWindowFactory, ILogger<WindowsArranger> logger)
        {
            _mainWindowFactory = mainWindowFactory ?? throw new ArgumentNullException(nameof(mainWindowFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IEnumerable<IPhoto> Photos => PhotoWindows.Select(x => x.Photo);

        IList<IPhotoWindow> PhotoWindows { get; } = new List<IPhotoWindow>();

        public void Add(IPhotoWindow window)
        {
            if (window == null)
            {
                throw new ArgumentNullException(nameof(window));
            }

            lock (_lockObject)
            {
                window.Closed += Window_Closed;
                var isFirst = !(PhotoWindows.Count > 0);
                PhotoWindows.Add(window);
                window.Photo.ReloadCollectionInfoIfNeeded();
                ArrangeWindowsAsync(isFirst);
            }
        }

        public void ClosePhoto(IPhoto photo)
        {
            if (photo == null)
            {
                throw new ArgumentNullException(nameof(photo));
            }

            lock (_lockObject)
            {
                for (var i = 0; i < PhotoWindows.Count; i++)
                {
                    var photoWindow = PhotoWindows[i];
                    if (photoWindow.Photo != photo)
                    {
                        continue;
                    }

                    _logger.LogTrace("Closing view for {Photo}...", photoWindow.Photo);
                    photoWindow.Close();
                    i--;
                }
            }
        }

        public void ClosePhotos()
        {
            lock (_lockObject)
            {
                _logger.LogTrace("Closing all views...");
                for (var i = 0; i < PhotoWindows.Count; i++)
                {
                    PhotoWindows[i].Close();
                    i--;
                }
            }
        }

        public void ToggleFullHeight(IPhotoWindow currentWindow)
        {
            if (currentWindow == null)
            {
                throw new ArgumentNullException(nameof(currentWindow));
            }

            lock (_lockObject)
            {
                if (PhotoWindows.Count > 1)
                {
                    var activeScreenArea = currentWindow.ActiveScreenArea;
                    var fullHeight = activeScreenArea.Height + WindowBorderWidth;
                    if (currentWindow.IsFullHeight)
                    {
                        ArrangeWindowsAsync(true);
                    }
                    else
                    {
                        foreach (var photoView in PhotoWindows)
                        {
                            photoView.Top = 0;
                            photoView.Height = fullHeight;
                        }
                    }
                }
                else
                {
                    ToggleFullScreen(currentWindow);
                }
            }
        }

        async void ArrangeWindowsAsync(bool defaultHeight = false)
        {
            _logger.LogTrace("Arranging windows...");
            var mainWindow = await _mainWindowFactory.GetWindowAsync(CancellationToken.None).ConfigureAwait(false);
            if (PhotoWindows.Count > 0)
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
                    firstPhotoHeight = twoThirdsHeight + (2 * WindowBorderWidth);
                    mainWindowHeight = thirdHeight;
                }
                else
                {
                    var firstPhoto = PhotoWindows[0];
                    firstPhotoTop = firstPhoto.Top;
                    firstPhotoHeight = firstPhoto.Height;
                    mainWindowHeight = activeScreenArea.Height - firstPhotoHeight + (2 * WindowBorderWidth);
                }

                _logger.LogTrace("Arranging as {MainWindowHeight:##.#}:{FirstPhotoHeight:##.#}...", mainWindowHeight, firstPhotoHeight);

                mainWindow.WindowState = WindowState.Normal;
                mainWindow.Top = activeScreenArea.Top;
                mainWindow.Width = activeScreenArea.Width + (2 * WindowBorderWidth);
                mainWindow.Left = activeScreenArea.Left - WindowBorderWidth;
                mainWindow.Height = mainWindowHeight;

                var actualWidth = width + (2 * WindowBorderWidth);
                foreach (var photoWindow in PhotoWindows)
                {
                    photoWindow.WindowState = WindowState.Normal;
                    photoWindow.Left = left - WindowBorderWidth;
                    left += width;
                    photoWindow.Width = actualWidth;
                    photoWindow.Top = firstPhotoTop;
                    photoWindow.Height = firstPhotoHeight;
                }

                if (PhotoWindows.Count > 0)
                {
                    PhotoWindows.Last().Restore();
                }
            }
            else
            {
                mainWindow.WindowState = WindowState.Maximized;
            }
        }

        void ToggleFullScreen(IResizableWindow window)
        {
            if (window.IsFullHeight)
            {
                _logger.LogTrace("Normalizing window view...");
                window.WindowState = WindowState.Normal;
                window.Topmost = false;
                window.WindowStyle = WindowStyle.SingleBorderWindow;
                window.ResizeMode = ResizeMode.CanResize;
                ArrangeWindowsAsync(true);
            }
            else
            {
                _logger.LogTrace("Maximizing window...");
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

        void Window_Closed(object? sender, EventArgs e)
        {
            if (sender is not IPhotoWindow window)
            {
                return;
            }

            window.Closed -= Window_Closed;

            lock (_lockObject)
            {
                PhotoWindows.Remove(window);
                if (PhotoWindows.Count == 1 && PhotoWindows.Single().IsFullHeight)
                {
                    ToggleFullScreen(PhotoWindows.Single());
                }
                else
                {
                    ArrangeWindowsAsync();
                }
            }

            GC.Collect();
        }
    }
}
