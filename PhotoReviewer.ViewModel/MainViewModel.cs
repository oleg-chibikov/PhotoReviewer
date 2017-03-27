using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Shell;
using Autofac;
using Common.Logging;
using GalaSoft.MvvmLight.Threading;
using JetBrains.Annotations;
using PhotoReviewer.Core;
using PhotoReviewer.DAL.Contracts;
using PhotoReviewer.Resources;
using PhotoReviewer.View.Contracts;
using PropertyChanged;
using Scar.Common.IO;
using Scar.Common.WPF;
using Scar.Common.WPF.Commands;

namespace PhotoReviewer.ViewModel
{
    //TODO: More logs
    //TODO: Localization
    //TODO: Manual rotate
    //TODO: Auto Rotate
    //TODO: Incorrect toggle fullheight when 1 image and almos full height
    //TODO: Detect photo change
    //TODO: Move directory watcher to separate class

    [ImplementPropertyChanged]
    public class MainViewModel : IDisposable
    {
        [NotNull]
        private readonly ILifetimeScope lifetimeScope;

        [NotNull]
        private readonly ILog logger;

        [NotNull]
        private readonly ISettingsRepository settingsRepository;

        [NotNull]
        private readonly WindowsArranger windowsArranger;

        [NotNull]
        private IEnumerable<Photo> selectedPhotos = new Photo[0];

        public MainViewModel([NotNull] PhotoCollection photoCollection, [NotNull] ISettingsRepository settingsRepository, [NotNull] ILifetimeScope lifetimeScope, [NotNull] WindowsArranger windowsArranger, [NotNull] ILog logger)
        {
            if (photoCollection == null)
                throw new ArgumentNullException(nameof(photoCollection));
            if (settingsRepository == null)
                throw new ArgumentNullException(nameof(settingsRepository));
            if (lifetimeScope == null)
                throw new ArgumentNullException(nameof(lifetimeScope));
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            PhotoCollection = photoCollection;
            this.settingsRepository = settingsRepository;
            this.lifetimeScope = lifetimeScope;
            this.windowsArranger = windowsArranger;
            this.logger = logger;
            BrowseDirectoryCommand = new CorrelationCommand(BrowseDirectory);
            ChangeDirectoryPathCommand = new CorrelationCommand<string>(ChangeDirectoryPath);
            ShowOnlyMarkedChangedCommand = new CorrelationCommand<bool>(ShowOnlyMarkedChanged);
            CopyFavoritedCommand = new CorrelationCommand(CopyFavorited);
            DeleteMarkedCommand = new CorrelationCommand(DeleteMarked);
            FavoriteCommand = new CorrelationCommand(Favorite);
            MarkForDeletionCommand = new CorrelationCommand(MarkForDeletion);
            RenameToDateCommand = new CorrelationCommand(RenameToDate);
            OpenPhotoInExplorerCommand = new CorrelationCommand(OpenPhotoInExplorer);
            OpenDirectoryInExplorerCommand = new CorrelationCommand(OpenDirectoryInExplorer);
            OpenPhotoCommand = new CorrelationCommand(OpenPhoto);
            SelectionChangedCommand = new CorrelationCommand<IList>(SelectionChanged);
            WindowClosingCommand = new CorrelationCommand(WindowClosing);
            OpenSettingsFolderCommand = new CorrelationCommand(OpenSettingsFolder);
            ViewLogsCommand = new CorrelationCommand(ViewLogs);
            PhotoCollection.Progress += PhotosCollection_Progress;
            PhotoCollection.CollectionChanged += PhotoCollection_CollectionChanged;

            var directoryPath = settingsRepository.Settings.LastUsedDirectoryPath;
            if (!string.IsNullOrWhiteSpace(directoryPath) && Directory.Exists(directoryPath))
                SetDirectoryPath(directoryPath);
        }

        [NotNull]
        public PhotoCollection PhotoCollection { get; }

        public void Dispose()
        {
            PhotoCollection.Progress -= PhotosCollection_Progress;
            PhotoCollection.CollectionChanged -= PhotoCollection_CollectionChanged;
        }

        private void PhotoCollection_CollectionChanged([NotNull] object sender, [NotNull] NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (SelectedPhoto == null)
                        SelectedPhoto = e.NewItems.Cast<Photo>().First();
                    //SelectionChanged is not hit sometimes
                    SelectionChanged(e.NewItems);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    foreach (var filePath in e.OldItems.Cast<Photo>().Select(x => x.FilePath).Distinct())
                        windowsArranger.ClosePhotos(filePath);
                    break;
            }
        }

        private void PhotosCollection_Progress([NotNull] object sender, [NotNull] ProgressEventArgs e)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                Progress = e.Percent;
                if (e.Percent == 100)
                    EndProgress();
            });
        }

        private void EndProgress()
        {
            ProgressState = TaskbarItemProgressState.None;
        }

        private void BeginProgress()
        {
            ProgressState = TaskbarItemProgressState.Normal;
            Progress = 0;
        }

        private void SetDirectoryPath([NotNull] string directoryPath)
        {
            var settings = settingsRepository.Settings;
            var task = PhotoCollection.SetDirectoryPathAsync(directoryPath);
            if (task.IsCompleted)
            {
                LogTaskException(task);
                //Restore previous path since current is corrupted
                CurrentPath = null;
                CurrentPath = settings.LastUsedDirectoryPath;
                return;
            }
            BeginProgress();
            windowsArranger.ClosePhotos();
            settings.LastUsedDirectoryPath = CurrentPath = directoryPath;
            settingsRepository.Settings = settings;
        }

        private void LogTaskException(Task task)
        {
            if (task.Exception != null)
                throw task.Exception;
        }

        /// <summary>
        /// Selects and scrolls to the first photo of current selection
        /// </summary>
        private void ReselectPhoto()
        {
            var photo = SelectedPhoto;
            SelectedPhoto = null;
            SelectedPhoto = photo;
        }

        #region Dependency Properties

        public int Progress { get; set; }

        public TaskbarItemProgressState ProgressState { get; set; }

        [CanBeNull]
        public string CurrentPath { get; set; }

        [CanBeNull]
        public Photo SelectedPhoto { get; set; }

        public int SelectedCount { get; set; }

        #endregion

        #region Commands

        [NotNull]
        public ICommand BrowseDirectoryCommand { get; }

        [NotNull]
        public ICommand ChangeDirectoryPathCommand { get; }

        [NotNull]
        public ICommand ShowOnlyMarkedChangedCommand { get; }

        [NotNull]
        public ICommand CopyFavoritedCommand { get; }

        [NotNull]
        public ICommand DeleteMarkedCommand { get; }

        [NotNull]
        public ICommand FavoriteCommand { get; }

        [NotNull]
        public ICommand MarkForDeletionCommand { get; }

        [NotNull]
        public ICommand OpenPhotoInExplorerCommand { get; }

        [NotNull]
        public ICommand OpenDirectoryInExplorerCommand { get; }

        [NotNull]
        public ICommand RenameToDateCommand { get; }

        [NotNull]
        public ICommand OpenPhotoCommand { get; }

        [NotNull]
        public ICommand SelectionChangedCommand { get; }

        [NotNull]
        public ICommand WindowClosingCommand { get; }

        [NotNull]
        public ICommand ViewLogsCommand { get; }

        [NotNull]
        public ICommand OpenSettingsFolderCommand { get; }

        #endregion

        #region Command handlers

        private void BrowseDirectory()
        {
            logger.Debug("Browsing directory...");
            //TODO: Another dialog third party? Use OpenFileService and DI
            var dialog = new FolderBrowserDialog();
            var lastUsedPath = settingsRepository.Settings.LastUsedDirectoryPath;

            if (!string.IsNullOrEmpty(lastUsedPath))
                dialog.SelectedPath = lastUsedPath;
            if (dialog.ShowDialog() != DialogResult.OK)
                return;
            logger.Info($"Changing directory path to {dialog.SelectedPath}...");
            SetDirectoryPath(dialog.SelectedPath);
        }

        private void ChangeDirectoryPath([NotNull] string directoryPath)
        {
            logger.Info($"Changing directory path to {directoryPath}...");
            if (directoryPath == null)
                throw new ArgumentNullException(nameof(directoryPath));
            SetDirectoryPath(directoryPath);
        }

        private void ShowOnlyMarkedChanged(bool isChecked)
        {
            logger.Debug($"Setting show only marked to {isChecked}...");
            PhotoCollection.ShowOnlyMarked = isChecked;
        }

        private void CopyFavorited()
        {
            logger.Info("Copying favorited photos...");
            var task = PhotoCollection.CopyFavoritedAsync();
            if (!task.IsCompleted)
                BeginProgress();
            else
                LogTaskException(task);
        }

        private void DeleteMarked()
        {
            logger.Info("Deleting marked photos...");
            var task = PhotoCollection.DeleteMarkedAsync();
            if (!task.IsCompleted)
                BeginProgress();
            else
                LogTaskException(task);
        }

        public void Favorite()
        {
            logger.Info("(Un)Favoriting selected photos...");
            if (!selectedPhotos.Any())
                throw new InvalidOperationException("No photo selected");
            PhotoCollection.Favorite(selectedPhotos.ToArray());
        }

        public void MarkForDeletion()
        {
            logger.Info("(Un)Marking selected photos for deletion...");
            if (!selectedPhotos.Any())
                throw new InvalidOperationException("No photo selected");
            PhotoCollection.MarkForDeletion(selectedPhotos.ToArray());
        }

        public void RenameToDate()
        {
            logger.Info("Renaming selected photos to date...");
            if (!selectedPhotos.Any())
                throw new InvalidOperationException("No photo selected");
            var task = PhotoCollection.RenameToDateAsync(selectedPhotos.ToArray());
            if (!task.IsCompleted)
                BeginProgress();
            else
                LogTaskException(task);
        }

        private void OpenPhotoInExplorer()
        {
            logger.Debug($"Opening selected photo ({SelectedPhoto?.FilePath}) in explorer...");
            if (SelectedPhoto == null)
                throw new InvalidOperationException("No photo selected");
            DirectoryUtility.OpenFileInExplorer(SelectedPhoto.FilePath);
        }

        private void OpenDirectoryInExplorer()
        {
            logger.Debug($"Opening current directory ({CurrentPath}) in explorer...");
            if (CurrentPath == null)
                throw new InvalidOperationException("No directory entered");
            DirectoryUtility.OpenDirectoryInExplorer(CurrentPath);
        }

        private void OpenPhoto()
        {
            logger.Debug($"Opening selected photo ({SelectedPhoto?.FilePath}) in a separate window...");
            if (SelectedPhoto == null)
                throw new InvalidOperationException("No photo selected");
            ReselectPhoto();
            var mainWindow = lifetimeScope.Resolve<WindowFactory<IMainWindow>>().GetWindow();
            var photoViewModel = lifetimeScope.Resolve<PhotoViewModel>(
                new TypedParameter(typeof(MainViewModel), this),
                new TypedParameter(typeof(Photo), SelectedPhoto)
            );
            //Window is shown in its constructor
            var window = lifetimeScope.Resolve<IPhotoWindow>(
                new TypedParameter(typeof(Window), mainWindow),
                new TypedParameter(typeof(PhotoViewModel), photoViewModel)
            );
            windowsArranger.Add(window);
        }

        private void SelectionChanged([NotNull] IList items)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));
            selectedPhotos = items.Cast<Photo>();
            SelectedCount = selectedPhotos.Count();
        }

        private void WindowClosing()
        {
            logger.Debug("Performing cleanup before dependencies disposal...");
            //Need to finish current task before disposal (especially, repository)
            PhotoCollection.CancelCurrentTask();
        }

        private void OpenSettingsFolder()
        {
            logger.Debug($"Opening setting folder ({Paths.SettingsPath})...");
            DirectoryUtility.OpenDirectoryInExplorer($@"{Paths.SettingsPath}");
        }

        private void ViewLogs()
        {
            var logsPath = $@"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}\{nameof(Scar)}\{nameof(PhotoReviewer)}\Logs\Full.log";
            logger.Debug($"Viewing logs file ({logsPath})...");
            DirectoryUtility.OpenFile(logsPath);
        }

        #endregion
    }
}