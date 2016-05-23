using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Animation;
using JetBrains.Annotations;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace PhotoReviewer
{
    public sealed partial class MainWindow
    {
        [NotNull]
        private static readonly DoubleAnimation HideAnimation = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = TimeSpan.FromSeconds(1)
        };

        private static readonly DoubleAnimation ShowAnimation = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromSeconds(1)
        };

        [NotNull]
        private static readonly Storyboard ProgressHideStoryBoard = new Storyboard { Children = new TimelineCollection { HideAnimation } };

        [NotNull]
        private static readonly Storyboard ProgressShowStoryBoard = new Storyboard { Children = new TimelineCollection { ShowAnimation } };

        [NotNull]
        private readonly FileSystemWatcher imagesDirectoryWatcher = new FileSystemWatcher
        {
            Filter = "*.jpg"
        };

        [NotNull]
        private readonly PhotoCollection photosCollection;

        [NotNull]
        private readonly IList<PhotoView> photoViews = new List<PhotoView>();

        private bool isInProgress;

        public MainWindow([NotNull] PhotoCollection photosCollection)
        {
            this.photosCollection = photosCollection;
            InitializeComponent();
            var path = Settings.Default.LastFolder;
            if (!string.IsNullOrEmpty(path))
                SetNewPath(path);
            photosCollection.Progress += PhotosCollection_Progress;
            imagesDirectoryWatcher.Created += ImagesDirectoryWatcher_Changed;
            imagesDirectoryWatcher.Deleted += ImagesDirectoryWatcher_Changed;
            imagesDirectoryWatcher.Renamed += ImagesDirectoryWatcher_Renamed;

            //ProgressBarContainer.Visibility=Visibility.Collapsed;

            Storyboard.SetTarget(HideAnimation, ProgressBarContainer);
            Storyboard.SetTargetProperty(HideAnimation, new PropertyPath(OpacityProperty));
            Storyboard.SetTarget(ShowAnimation, ProgressBarContainer);
            Storyboard.SetTargetProperty(ShowAnimation, new PropertyPath(OpacityProperty));
        }

        #region Events

        private void ViewPhotoMenuItem_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
        {
            OpenView();
        }

        private void MarkAsDeletedMenuItem_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
        {
            MarkAsDeleted();
        }

        private void FavoriteMenuItem_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
        {
            Favorite();
        }

        private void OpenInExplorerMenuItem_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
        {
            OpenInExplorer(((Photo)PhotosListBox.SelectedItem).Path);
        }

        private void RenameToDateMenuItem_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
        {
            RenameToDate();
        }

        private void BrowseDirectoryButton_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();

            if (!string.IsNullOrEmpty(Settings.Default.LastFolder))
                dialog.SelectedPath = Settings.Default.LastFolder;
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;
            SetNewPath(dialog.SelectedPath);
        }

        private void DeleteButton_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
        {
            if (!BeginProgress())
                return;
            photosCollection.DeleteMarked(CloseViews);
        }

        private void MoveFavoritedButton_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
        {
            if (!BeginProgress())
                return;
            photosCollection.MoveFavorited();
        }

        private void PhotosListBox_PreviewKeyDown([NotNull] object sender, [NotNull] KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.R:
                    if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                        RenameToDate();
                    break;
                case Key.Delete:
                case Key.Back:
                    MarkAsDeleted();
                    break;
                case Key.F:
                    if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                        Favorite();
                    break;
                case Key.Enter:
                    OpenView();
                    break;
            }
        }

        private void ImagesDirTextBox_KeyDown([NotNull] object sender, [NotNull] KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;
            SetNewPath(ImagesDirTextBox.Text);
        }

        private void ImagesDirectoryWatcher_Changed([NotNull] object sender, [NotNull] FileSystemEventArgs fileSystemEventArgs)
        {
            Dispatcher.Invoke(() =>
            {
                var path = fileSystemEventArgs.FullPath;
                switch (fileSystemEventArgs.ChangeType)
                {
                    case WatcherChangeTypes.Deleted:
                        photosCollection.DeletePhoto(path);
                        CloseViews(path);
                        break;
                    case WatcherChangeTypes.Created:
                        photosCollection.AddPhoto(path);
                        break;
                }
            });
        }

        private void ImagesDirectoryWatcher_Renamed([NotNull] object sender, [NotNull] RenamedEventArgs renamedEventArgs)
        {
            Dispatcher.Invoke(() => { photosCollection.RenamePhoto(renamedEventArgs.OldFullPath, renamedEventArgs.FullPath); });
        }

        private void PhotosCollection_Progress(object sender, PhotoCollection.ProgressEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressBar.Value = e.Percent;
                if (e.Percent == 100)
                    EndProgress();
            });
        }

        #endregion

        #region Public

        public static void OpenInExplorer([NotNull] string filePath)
        {
            new Process
            {
                StartInfo =
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{filePath}\""
                }
            }.Start();
        }

        public void ScrollToSelected()
        {
            PhotosListBox.ScrollIntoView(PhotosListBox.SelectedItem);
        }

        public void MarkAsDeleted()
        {
            if (!BeginProgress())
                return;
            photosCollection.MarkForDeletion(PhotosListBox.SelectedItems.Cast<Photo>().ToArray());
        }

        public void Favorite()
        {
            if (!BeginProgress())
                return;
            photosCollection.Favorite(PhotosListBox.SelectedItems.Cast<Photo>().ToArray());
        }

        public void RenameToDate()
        {
            if (!BeginProgress())
                return;
            photosCollection.RenameToDate(PhotosListBox.SelectedItems.Cast<Photo>().ToArray(), path =>
            {
                var i = 0;
                Photo lastRenamed = null;
                while (i++ < 5 && (lastRenamed = photosCollection.SingleOrDefault(x => x.Path == path)) == null)
                    Thread.Sleep(100);
                PhotosListBox.SelectedItem = lastRenamed;
                ScrollToSelected();
            });
        }

        #endregion

        #region Private

        private bool BeginProgress()
        {
            if (isInProgress)
                return false;
            MoveFavoritedButton.IsEnabled = false;
            DeleteButton.IsEnabled = false;
            BrowseDirectoryButton.IsEnabled = false;
            ImagesDirTextBox.IsEnabled = false;
            isInProgress = true;
            ProgressBar.Value = 0;
            // ProgressBarContainer.Visibility = Visibility.Visible;
            ProgressShowStoryBoard.Begin();
            return true;
        }

        private void EndProgress()
        {
            MoveFavoritedButton.IsEnabled = true;
            DeleteButton.IsEnabled = true;
            BrowseDirectoryButton.IsEnabled = true;
            ImagesDirTextBox.IsEnabled = true;
            isInProgress = false;
            ProgressBar.Value = 0;
            //ProgressBarContainer.Visibility=Visibility.Collapsed;
            ProgressHideStoryBoard.Begin();
        }

        private void SetNewPath([NotNull] string path)
        {
            imagesDirectoryWatcher.EnableRaisingEvents = false;
            imagesDirectoryWatcher.Path = photosCollection.Path = Settings.Default.LastFolder = ImagesDirTextBox.Text = path;
            imagesDirectoryWatcher.EnableRaisingEvents = true;
            Settings.Default.Save();
            if (PhotosListBox.HasItems)
                PhotosListBox.SelectedIndex = 0;
        }

        private void CloseViews(string path)
        {
            for (var i = 0; i < photoViews.Count; i++)
            {
                var view = photoViews[i];
                if (view.SelectedPhoto.Path == path)
                {
                    view.Close();
                    photoViews.Remove(view);
                    i--;
                }
            }
        }

        private void OpenView()
        {
            // ReSharper disable once ObjectCreationAsStatement
            new PhotoView((Photo)PhotosListBox.SelectedItem, photoViews, this) { Owner = this };
        }

        #endregion
    }
}