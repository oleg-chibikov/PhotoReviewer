using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using JetBrains.Annotations;
using Microsoft.VisualBasic.FileIO;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;

namespace PhotoReviewer
{
    public sealed partial class MainWindow
    {

        [NotNull]
        private readonly IList<PhotoView> photoViews = new List<PhotoView>();

        [NotNull]
        private readonly PhotoCollection photosCollection;

        [NotNull]
        private readonly FileSystemWatcher imagesDirectoryWatcher = new FileSystemWatcher
        {
            Filter = "*.jpg"
        };

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
            new PhotoView((Photo)PhotosListBox.SelectedItem, photoViews) { Owner = this };
        }

        private void MarkAsDeletedMenuItem_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
        {
            MarkAsDeleted();
        }

        private void FavoriteMenuItem_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
        {
            Favorite();
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
            var photos = photosCollection.Where(x => x.MarkedForDeletion).ToArray();
            if (!photos.Any())
            {
                MessageBox.Show("Nothing to delete");
                return;
            }
            foreach (var photo in photos)
            {
                photosCollection.Remove(photo);
                if (File.Exists(photo.Source))
                {
                    FileSystem.DeleteFile(photo.Source,
                        UIOption.OnlyErrorDialogs,
                        RecycleOption.SendToRecycleBin);
                }
                DbProvider.Delete(photo.Source, DbProvider.OperationType.MarkForDeletion);
                DbProvider.Delete(photo.Source, DbProvider.OperationType.Favorite);
            }
        }

        private void MoveFavoritedButton_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
        {
            var photos = photosCollection.Where(x => x.Favorited).ToArray();
            if (!photos.Any())
            {
                MessageBox.Show("Nothing to move");
                return;
            }
            var dir = Path.GetDirectoryName(photos.First().Source) + "\\Favorite\\";
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            foreach (var photo in photos)
            {
                if (!File.Exists(photo.Source))
                    continue;
                var newName = dir + Path.GetFileName(photo.Source);
                if (!File.Exists(newName))
                    File.Copy(photo.Source, newName);
            }
            Process.Start(dir);
        }

        private void OpenInExplorerMenuItem_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
        {
            OpenInExplorer(((Photo)PhotosListBox.SelectedItem).Source);
        }

        private void RenameToDateMenuItem_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
        {
            RenameToDate();
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
            Dispatcher.Invoke(() =>
            {
                photosCollection.RenamePhoto(renamedEventArgs.OldFullPath, renamedEventArgs.FullPath);
            });
        }

        #endregion

        #region Private

        private static void OpenInExplorer([NotNull] string filePath)
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

        private void MarkAsDeleted()
        {
            var photos = PhotosListBox.SelectedItems;
            foreach (Photo photo in photos)
                photo.MarkForDeletion();
        }

        private void Favorite()
        {
            var photos = PhotosListBox.SelectedItems;
            foreach (Photo photo in photos)
                photo.Favorite();
        }

        private void RenameToDate()
        {
            var photos = PhotosListBox.SelectedItems.Cast<Photo>().ToArray();
            if (!photos.Any())
            {
                MessageBox.Show("Nothing to rename");
                return;
            }
            var dir = Path.GetDirectoryName(photos.First().Source);
            string newPath = null;
            foreach (var photo in photos)
            {
                var oldPath = photo.Source;
                if (!File.Exists(photo.Source) || !photo.Metadata.DateImageTaken.HasValue)
                    continue;
                var newName = photo.Metadata.DateImageTaken.Value.ToString("yyyy-MM-dd hh-mm-ss");
                newPath = GetFreeFileName($"{dir}\\{newName}.jpg");
                if (!File.Exists(newPath))
                    File.Move(oldPath, newPath);
                //FileSystemWatcher will do the rest
            }
            if (newPath != null)
                OpenInExplorer(newPath);
            var context = SynchronizationContext.Current;
            //New thread is required to release current method and trigger fileSystemWatcher
            Task.Run(() =>
            {
                context.Send(t =>
                {
                    Photo lastRenamed;
                    while ((lastRenamed = photosCollection.SingleOrDefault(x => x.Source == newPath)) == null)
                        Thread.Sleep(100);
                    PhotosListBox.SelectedItem = lastRenamed;
                }, null);
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

        [NotNull]
        private string GetFreeFileName([NotNull]string fullPath)
        {
            var count = 1;

            var fileNameOnly = Path.GetFileNameWithoutExtension(fullPath);
            var extension = Path.GetExtension(fullPath);
            var path = Path.GetDirectoryName(fullPath);
            if (path == null)
                throw new ArgumentException(nameof(fullPath));
            var newFullPath = fullPath;

            while (File.Exists(newFullPath))
            {
                var tempFileName = $"{fileNameOnly} ({count++})";
                newFullPath = Path.Combine(path, tempFileName + extension);
            }
            return newFullPath;
        }

        #endregion
    }
}