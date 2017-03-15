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
using System.Windows.Shell;
using GalaSoft.MvvmLight.Messaging;
using JetBrains.Annotations;
using PhotoReviewer.DAL.Contracts;
using PhotoReviewer.Resources;
using PhotoReviewer.ViewModel;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace PhotoReviewer.View
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

        //TODO: Dispose and unbind events
        [NotNull]
        private readonly FileSystemWatcher imagesDirectoryWatcher = new FileSystemWatcher
        {
            //TODO: polling every n seconds or use queue for handlers
            InternalBufferSize = 64 * 1024
        };

        [NotNull]
        private readonly IMessenger messenger;

        [NotNull]
        private readonly PhotoCollection photoCollection;

        [NotNull]
        private readonly IList<PhotoView> photoViews = new List<PhotoView>();

        [NotNull]
        private readonly ISettingsRepository settingsRepository;

        private bool isInProgress;

        public MainWindow([NotNull] PhotoCollection photoCollection, [NotNull] ISettingsRepository settingsRepository, [NotNull] IMessenger messenger)
        {
            if (photoCollection == null)
                throw new ArgumentNullException(nameof(photoCollection));
            if (settingsRepository == null)
                throw new ArgumentNullException(nameof(settingsRepository));
            if (messenger == null)
                throw new ArgumentNullException(nameof(messenger));
            this.photoCollection = photoCollection;
            this.settingsRepository = settingsRepository;
            this.messenger = messenger;
            DataContext = photoCollection;
            InitializeComponent();
            var path = settingsRepository.Get().LastUsedPath;
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                SetNewPath(path);
            photoCollection.Progress += PhotosCollection_Progress;
            photoCollection.PhotoDeleted += PhotoCollection_PhotoDeleted;
            imagesDirectoryWatcher.Created += ImagesDirectoryWatcher_Changed;
            imagesDirectoryWatcher.Deleted += ImagesDirectoryWatcher_Changed;
            imagesDirectoryWatcher.Renamed += ImagesDirectoryWatcher_Renamed;

            //ProgressBarContainer.Visibility=Visibility.Collapsed;

            Storyboard.SetTarget(HideAnimation, ProgressBarContainer);
            Storyboard.SetTargetProperty(HideAnimation, new PropertyPath(OpacityProperty));
            Storyboard.SetTarget(ShowAnimation, ProgressBarContainer);
            Storyboard.SetTargetProperty(ShowAnimation, new PropertyPath(OpacityProperty));
        }

        private void PhotoCollection_PhotoDeleted(object sender, PhotoDeletedEventArgs e)
        {
            Dispatcher.Invoke(() => { CloseViews(e.Path); });
        }

        private void ShowOnlyMarkedCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            photoCollection.ShowOnlyMarked = true;
        }

        private void ShowOnlyMarkedCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            photoCollection.ShowOnlyMarked = false;
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
            OpenFileInExplorer(((Photo)PhotosListBox.SelectedItem).FilePath);
        }

        private void RenameToDateMenuItem_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
        {
            RenameToDate();
        }

        private void BrowseDirectoryButton_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            var lastUsedPath = settingsRepository.Get().LastUsedPath;

            if (!string.IsNullOrEmpty(lastUsedPath))
                dialog.SelectedPath = lastUsedPath;
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;
            SetNewPath(dialog.SelectedPath);
        }

        private void OpenInExplorerButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(ImagesDirTextBox.Text);
        }

        private void DeleteButton_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
        {
            if (!BeginProgress())
                return;
            photoCollection.DeleteMarked();
        }

        private void MoveFavoritedButton_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
        {
            if (!BeginProgress())
                return;
            photoCollection.MoveFavorited();
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
            var path = fileSystemEventArgs.FullPath;
            if (!IsImage(path))
                return;

            Dispatcher.Invoke(() =>
            {
                switch (fileSystemEventArgs.ChangeType)
                {
                    case WatcherChangeTypes.Deleted:
                        photoCollection.DeletePhoto(path);
                        break;
                    case WatcherChangeTypes.Created:
                        photoCollection.GetDetailsAndAddPhoto(path);
                        break;
                }
            });
        }

        private bool IsImage(string path)
        {
            return Constants.FileExtensions.Contains(Path.GetExtension(path));
        }

        private void ImagesDirectoryWatcher_Renamed([NotNull] object sender, [NotNull] RenamedEventArgs renamedEventArgs)
        {
            if (!IsImage(renamedEventArgs.FullPath))
                return;
            Dispatcher.Invoke(() => { photoCollection.RenamePhoto(renamedEventArgs.OldFullPath, renamedEventArgs.FullPath); });
        }

        private void PhotosCollection_Progress(object sender, ProgressEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressBar.Value = e.Percent;
                TaskbarItemInfo.ProgressValue = (double)e.Percent / 100;
                if (e.Percent == 100)
                    EndProgress();
            });
        }

        #endregion

        #region Public

        public static void OpenFileInExplorer([NotNull] string filePath)
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
            photoCollection.MarkForDeletion(PhotosListBox.SelectedItems.Cast<Photo>().ToArray());
        }

        public void Favorite()
        {
            if (!BeginProgress())
                return;
            photoCollection.Favorite(PhotosListBox.SelectedItems.Cast<Photo>().ToArray());
        }

        public async void RenameToDate()
        {
            if (!BeginProgress())
                return;
            var newPath = await photoCollection.RenameToDateAsync(PhotosListBox.SelectedItems.Cast<Photo>().ToArray());
            if (newPath == null)
                return;
            var i = 0;
            Photo lastRenamed = null;
            while (i++ < 5 && (lastRenamed = photoCollection.SingleOrDefault(x => x.FilePath == newPath)) == null)
                Thread.Sleep(100);
            PhotosListBox.SelectedItem = lastRenamed;
            ScrollToSelected();
        }

        #endregion

        #region Private

        private bool BeginProgress()
        {
            if (isInProgress)
                return false;
            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
            MoveFavoritedButton.IsEnabled = false;
            DeleteButton.IsEnabled = false;
            BrowseDirectoryButton.IsEnabled = false;
            ImagesDirTextBox.IsEnabled = false;
            isInProgress = true;
            ProgressBar.Value = 0;
            TaskbarItemInfo.ProgressValue = 0;
            // ProgressBarContainer.Visibility = Visibility.Visible;
            ProgressShowStoryBoard.Begin();
            return true;
        }

        private void EndProgress()
        {
            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
            MoveFavoritedButton.IsEnabled = true;
            DeleteButton.IsEnabled = true;
            BrowseDirectoryButton.IsEnabled = true;
            ImagesDirTextBox.IsEnabled = true;
            isInProgress = false;
            //ProgressBarContainer.Visibility=Visibility.Collapsed;
            ProgressHideStoryBoard.Begin();
        }

        private void SetNewPath([NotNull] string path)
        {
            var settings = settingsRepository.Get();
            if (!Directory.Exists(path))
            {
                messenger.Send(string.Format(Errors.DirecoryDoesNotExist, path), MessengerTokens.UserWarningToken);
                ImagesDirTextBox.Text = settings.LastUsedPath;
                return;
            }
            CloseViews();
            imagesDirectoryWatcher.EnableRaisingEvents = false;
            imagesDirectoryWatcher.Path = photoCollection.Path = settings.LastUsedPath = ImagesDirTextBox.Text = path;
            imagesDirectoryWatcher.EnableRaisingEvents = true;
            settingsRepository.Save(settings);
            if (PhotosListBox.HasItems)
                PhotosListBox.SelectedIndex = 0;
        }

        private void CloseViews([CanBeNull] string path = null)
        {
            for (var i = 0; i < photoViews.Count; i++)
            {
                var view = photoViews[i];
                if (path == null || view.SelectedPhoto.FilePath == path)
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