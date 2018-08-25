using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Common.Logging;
using JetBrains.Annotations;
using PhotoReviewer.Contracts.View;
using PhotoReviewer.Contracts.ViewModel;
using Scar.Common.WPF.View;
using Scar.Common.WPF.View.Contracts;

namespace PhotoReviewer.Core
{
    [UsedImplicitly]
    public sealed class WindowsArranger
    {
        private const double WindowBorderWidth = 8;

        private readonly object _lockObject = new object();

        [NotNull]
        private readonly ILog _logger;

        [NotNull]
        private readonly WindowFactory<IMainWindow> _mainWindowFactory;

        public WindowsArranger([NotNull] WindowFactory<IMainWindow> mainWindowFactory, [NotNull] ILog logger)
        {
            _mainWindowFactory = mainWindowFactory ?? throw new ArgumentNullException(nameof(mainWindowFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [NotNull]
        private IList<IPhotoWindow> PhotoWindows { get; } = new List<IPhotoWindow>();

        [NotNull]
        public IEnumerable<IPhoto> Photos => PhotoWindows.Select(x => x.Photo);

        public void Add([NotNull] IPhotoWindow window)
        {
            if (window == null)
                throw new ArgumentNullException(nameof(window));

            lock (_lockObject)
            {
                window.Closed += Window_Closed;
                var isFirst = !PhotoWindows.Any();
                PhotoWindows.Add(window);
                window.Photo.ReloadCollectionInfoIfNeeded();
                ArrangeWindowsAsync(isFirst);
            }
        }

        public void ClosePhoto([NotNull] IPhoto photo)
        {
            if (photo == null)
                throw new ArgumentNullException(nameof(photo));

            lock (_lockObject)
            {
                for (var i = 0; i < PhotoWindows.Count; i++)
                {
                    var photoWindow = PhotoWindows[i];
                    if (photoWindow.Photo != photo)
                        continue;

                    _logger.Trace($"Closing view for {photoWindow.Photo}...");
                    photoWindow.Close();
                    i--;
                }
            }
        }

        public void ClosePhotos()
        {
            lock (_lockObject)
            {
                _logger.Trace("Closing all views...");
                for (var i = 0; i < PhotoWindows.Count; i++)
                {
                    PhotoWindows[i].Close();
                    i--;
                }
            }
        }

        public void ToggleFullHeight([NotNull] IPhotoWindow currentWindow)
        {
            if (currentWindow == null)
                throw new ArgumentNullException(nameof(currentWindow));

            lock (_lockObject)
            {
                if (PhotoWindows.Count > 1)
                {
                    var activeScreenArea = currentWindow.ActiveScreenArea;
                    var fullHeight = activeScreenArea.Height + WindowBorderWidth;
                    if (currentWindow.IsFullHeight)
                        ArrangeWindowsAsync(true);
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
        }

        #region Private

        private async void ArrangeWindowsAsync(bool defaultHeight = false)
        {
            _logger.Trace("Arranging windows...");
            var mainWindow = await _mainWindowFactory.GetWindowAsync(CancellationToken.None).ConfigureAwait(false);
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
                _logger.Trace($"Arranging as {mainWindowHeight:##.#}:{firstPhotoHeight:##.#}...");

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

        private void ToggleFullScreen([NotNull] IWindow window)
        {
            if (window.IsFullHeight)
            {
                _logger.Trace("Normalizing window view...");
                window.WindowState = WindowState.Normal;
                window.Topmost = false;
                window.WindowStyle = WindowStyle.SingleBorderWindow;
                window.ResizeMode = ResizeMode.CanResize;
                ArrangeWindowsAsync(true);
            }
            else
            {
                _logger.Trace("Maximizing window...");
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

        private void Window_Closed(object sender, EventArgs e)
        {
            var window = (IPhotoWindow) sender;
            window.Closed -= Window_Closed;

            lock (_lockObject)
            {
                PhotoWindows.Remove(window);
                if (PhotoWindows.Count == 1 && PhotoWindows.Single().IsFullHeight)
                    ToggleFullScreen(PhotoWindows.Single());
                else
                    ArrangeWindowsAsync();
            }
            GC.Collect();
        }

        #endregion
    }
}