using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using JetBrains.Annotations;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace PhotoReviewer
{
    public sealed partial class MainWindow
    {
        [NotNull]
        private readonly FileSystemWatcher imagesDirectoryWatcher = new FileSystemWatcher
        {
            Filter = "*.jpg"
        };

        [NotNull]
        private readonly PhotoCollection photosCollection;

        [NotNull]
        private readonly IList<PhotoView> photoViews = new List<PhotoView>();

        public MainWindow([NotNull] PhotoCollection photosCollection)
        {
            this.photosCollection = photosCollection;
            InitializeComponent();
            var path = Settings.Default.LastFolder;
            if (!string.IsNullOrEmpty(path))
                SetNewPath(path);
            imagesDirectoryWatcher.Created += ImagesDirectoryWatcher_Changed;
            imagesDirectoryWatcher.Deleted += ImagesDirectoryWatcher_Changed;
            imagesDirectoryWatcher.Renamed += ImagesDirectoryWatcher_Renamed;
        }

        #region Events

        private void ViewPhotoMenuItem_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
        {
            // ReSharper disable once ObjectCreationAsStatement
            new PhotoView((Photo)PhotosListBox.SelectedItem, photoViews, this) { Owner = this };
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
            photosCollection.DeleteMarked();
        }

        private void MoveFavoritedButton_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
        {
            photosCollection.MoveFavorited();
        }

        private void PhotosListBox_KeyDown([NotNull] object sender, [NotNull] KeyEventArgs e)
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
                case Key.Enter:
                    Favorite();
                    break;
            }
        }

        private void ImagesDir_KeyDown([NotNull] object sender, [NotNull] KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;
            SetNewPath(ImagesDir.Text);
        }

        private void ImagesDirectoryWatcher_Changed([NotNull] object sender, [NotNull] FileSystemEventArgs fileSystemEventArgs)
        {
            Dispatcher.Invoke(() =>
            {
                switch (fileSystemEventArgs.ChangeType)
                {
                    case WatcherChangeTypes.Deleted:
                        photosCollection.DeletePhoto(fileSystemEventArgs.FullPath);
                        break;
                    case WatcherChangeTypes.Created:
                        photosCollection.AddPhoto(fileSystemEventArgs.FullPath);
                        break;
                }
            });
        }

        private void ImagesDirectoryWatcher_Renamed([NotNull] object sender, [NotNull] RenamedEventArgs renamedEventArgs)
        {
            Dispatcher.Invoke(() => { photosCollection.RenamePhoto(renamedEventArgs.OldFullPath, renamedEventArgs.FullPath); });
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

        #endregion

        #region Private

        public void MarkAsDeleted()
        {
            photosCollection.MarkForDeletion(PhotosListBox.SelectedItems.Cast<Photo>().ToArray());
        }

        public void Favorite()
        {
            photosCollection.Favorite(PhotosListBox.SelectedItems.Cast<Photo>().ToArray());
        }

        public void RenameToDate()
        {
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

        private void SetNewPath([NotNull] string path)
        {
            imagesDirectoryWatcher.EnableRaisingEvents = false;
            imagesDirectoryWatcher.Path = photosCollection.Path = Settings.Default.LastFolder = ImagesDir.Text = path;
            imagesDirectoryWatcher.EnableRaisingEvents = true;
            Settings.Default.Save();
            if (PhotosListBox.HasItems)
                PhotosListBox.SelectedIndex = 0;
        }

        #endregion
    }
}