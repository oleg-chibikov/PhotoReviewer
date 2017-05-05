using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Shell;
using Autofac;
using Common.Logging;
using JetBrains.Annotations;
using PhotoReviewer.Contracts.DAL;
using PhotoReviewer.Contracts.View;
using PhotoReviewer.Core;
using PhotoReviewer.Resources;
using PropertyChanged;
using Scar.Common.Events;
using Scar.Common.IO;
using Scar.Common.WPF.Commands;
using Scar.Common.WPF.View;

namespace PhotoReviewer.ViewModel
{
    //TODO: More logs
    //TODO: Localization
    //TODO: Manual rotate
    //TODO: Auto Rotate
    //TODO: Incorrect toggle fullheight when 1 image and almos full height
    //TODO: Detect photo change
    //TODO: Move directory watcher to separate class
    //TODO: Incorrect multiple photos selection when over one screen
    //TODO: Disable context menu items while performing an operation
    //TODO: Change Path can only cancel another same operation

    [ImplementPropertyChanged]
    public sealed class MainViewModel : IDisposable
    {
        [NotNull]
        private readonly SynchronizationContext _syncContext = SynchronizationContext.Current;
        [NotNull] private readonly ILifetimeScope _lifetimeScope;

        [NotNull] private readonly ILog _logger;

        [NotNull] private readonly ISettingsRepository _settingsRepository;

        [NotNull] private readonly WindowsArranger _windowsArranger;

        public MainViewModel([NotNull] PhotoCollection photoCollection,
            [NotNull] ISettingsRepository settingsRepository,
            [NotNull] ILifetimeScope lifetimeScope,
            [NotNull] WindowsArranger windowsArranger,
            [NotNull] ILog logger)
        {
            PhotoCollection = photoCollection ?? throw new ArgumentNullException(nameof(photoCollection));
            _settingsRepository = settingsRepository ?? throw new ArgumentNullException(nameof(settingsRepository));
            _lifetimeScope = lifetimeScope ?? throw new ArgumentNullException(nameof(lifetimeScope));
            _windowsArranger = windowsArranger;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            BrowseDirectoryCommand = new CorrelationCommand(BrowseDirectoryAsync);
            ChangeDirectoryPathCommand = new CorrelationCommand<string>(ChangeDirectoryPathAsync);
            ShowOnlyMarkedChangedCommand = new CorrelationCommand<bool>(ShowOnlyMarkedChanged);
            CopyFavoritedCommand = new CorrelationCommand(CopyFavoritedAsync);
            DeleteMarkedCommand = new CorrelationCommand(DeleteMarkedAsync);
            FavoriteCommand = new CorrelationCommand(Favorite);
            MarkForDeletionCommand = new CorrelationCommand(MarkForDeletion);
            RenameToDateCommand = new CorrelationCommand(RenameToDateAsync);
            OpenPhotoInExplorerCommand = new CorrelationCommand(OpenPhotoInExplorer);
            OpenDirectoryInExplorerCommand = new CorrelationCommand(OpenDirectoryInExplorer);
            OpenPhotoCommand = new CorrelationCommand(OpenPhoto);
            SelectionChangedCommand = new CorrelationCommand<IList>(SelectionChanged);
            WindowClosingCommand = new CorrelationCommand(WindowClosing);
            OpenSettingsFolderCommand = new CorrelationCommand(OpenSettingsFolder);
            ViewLogsCommand = new CorrelationCommand(ViewLogs);
            PhotoCollection.Progress += PhotosCollection_Progress;
            PhotoCollection.CollectionChanged += PhotoCollection_CollectionChanged;
            PhotoCollection.PhotoNotification += PhotoCollection_PhotoNotification;
            ShiftDateViewModel =
                lifetimeScope.Resolve<ShiftDateViewModel>(new TypedParameter(typeof(MainViewModel), this));

            var directoryPath = settingsRepository.Settings.LastUsedDirectoryPath;
            if (!string.IsNullOrWhiteSpace(directoryPath) && Directory.Exists(directoryPath))
#pragma warning disable 4014
                SetDirectoryPathAsync(directoryPath);
#pragma warning restore 4014
        }

        private void PhotoCollection_PhotoNotification(object sender, EventArgs e)
        {
            foreach (var photo in _windowsArranger.Photos)
                photo.ReloadCollectionInfoIfNeeded();
            SelectedPhoto?.ReloadCollectionInfoIfNeeded();
        }

        [NotNull]
        internal IEnumerable<Photo> SelectedPhotos { get; private set; } = new Photo[0];

        [NotNull]
        public PhotoCollection PhotoCollection { get; }

        [NotNull]
        public ShiftDateViewModel ShiftDateViewModel { get; }

        public void Dispose()
        {
            PhotoCollection.Progress -= PhotosCollection_Progress;
            PhotoCollection.PhotoNotification -= PhotoCollection_PhotoNotification;
            PhotoCollection.CollectionChanged -= PhotoCollection_CollectionChanged;
        }

        private void PhotoCollection_CollectionChanged([NotNull] object sender,
            [NotNull] NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (SelectedPhoto == null)
                    {
                        SelectedPhoto = e.NewItems.Cast<Photo>().First();
                        //SelectionChanged is not hit sometimes
                        SelectionChanged(e.NewItems);
                    }
                    break;
                case NotifyCollectionChangedAction.Remove:
                    foreach (var photo in e.OldItems.Cast<Photo>())
                        _windowsArranger.ClosePhotos(photo);

                    break;
            }
        }

        private void PhotosCollection_Progress([NotNull] object sender, [NotNull] ProgressEventArgs e)
        {
            _syncContext.Send(x =>
            {
                Progress = e.Percentage;
                ProgressDescription = $"{e.Current} of {e.Total} ({e.Percentage} %)";
                if (e.Current == 0)
                    BeginProgress();
                else if (e.Current == e.Total)
                    EndProgress();
            }, null);
        }

        private void EndProgress()
        {
            ProgressState = TaskbarItemProgressState.None;
        }

        private void BeginProgress()
        {
            ProgressState = TaskbarItemProgressState.Normal;
            ProgressDescription = "Caclulating...";
            Progress = 0;
        }

        private async Task SetDirectoryPathAsync([NotNull] string directoryPath)
        {
            var settings = _settingsRepository.Settings;
            var task = PhotoCollection.SetDirectoryPathAsync(directoryPath);

            if (task.IsCompleted || task.IsFaulted)
            {
                //Restore previous path since current is corrupted
                CurrentDirectoryPath = null;
                CurrentDirectoryPath = settings.LastUsedDirectoryPath;
            }
            else
            {
                _windowsArranger.ClosePhotos();
                settings.LastUsedDirectoryPath = CurrentDirectoryPath = directoryPath;
                _settingsRepository.Settings = settings;
            }
            await task;
        }

        /// <summary>
        ///     Selects and scrolls to the first photo of current selection
        /// </summary>
        private void ReselectPhoto()
        {
            var photo = SelectedPhoto;
            SelectedPhoto = null;
            SelectedPhoto = photo;
        }

        #region Dependency Properties

        public double PhotoSize { get; set; } = 230;

        public int Progress { get; set; }

        public string ProgressDescription { get; set; }

        public TaskbarItemProgressState ProgressState { get; set; }

        [CanBeNull]
        public string CurrentDirectoryPath { get; set; }

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

        private async void BrowseDirectoryAsync()
        {
            _logger.Debug("Browsing directory...");
            //TODO: Another dialog third party? Use OpenFileService and DI
            var dialog = new FolderBrowserDialog();
            var lastUsedPath = _settingsRepository.Settings.LastUsedDirectoryPath;

            if (!string.IsNullOrEmpty(lastUsedPath))
                dialog.SelectedPath = lastUsedPath;
            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            _logger.Info($"Changing directory path to {dialog.SelectedPath}...");
            await SetDirectoryPathAsync(dialog.SelectedPath);
        }

        private async void ChangeDirectoryPathAsync([NotNull] string directoryPath)
        {
            _logger.Info($"Changing directory path to {directoryPath}...");
            if (directoryPath == null)
                throw new ArgumentNullException(nameof(directoryPath));

            await SetDirectoryPathAsync(directoryPath.AddTrailingBackslash());
        }

        private void ShowOnlyMarkedChanged(bool isChecked)
        {
            _logger.Debug($"Setting show only marked to {isChecked}...");
            PhotoCollection.ShowOnlyMarked = isChecked;
        }

        private async void CopyFavoritedAsync()
        {
            _logger.Info("Copying favorited photos...");
            await PhotoCollection.CopyFavoritedAsync();
        }

        private async void DeleteMarkedAsync()
        {
            _logger.Info("Deleting marked photos...");
            await PhotoCollection.DeleteMarkedAsync();
        }

        internal void Favorite()
        {
            _logger.Info("(Un)Favoriting selected photos...");
            if (!SelectedPhotos.Any())
                throw new InvalidOperationException("Photos are not selected");

            PhotoCollection.Favorite(SelectedPhotos.ToArray());
        }

        internal void MarkForDeletion()
        {
            _logger.Info("(Un)Marking selected photos for deletion...");
            if (!SelectedPhotos.Any())
                throw new InvalidOperationException("Photos are not selected");

            PhotoCollection.MarkForDeletion(SelectedPhotos.ToArray());
        }

        internal async void RenameToDateAsync()
        {
            _logger.Info("Renaming selected photos to date...");
            if (!SelectedPhotos.Any())
                throw new InvalidOperationException("Photos are not selected");

            await PhotoCollection.RenameToDateAsync(SelectedPhotos.ToArray());
        }

        private void OpenPhotoInExplorer()
        {
            _logger.Debug($"Opening selected photo ({SelectedPhoto?.FilePath}) in explorer...");
            if (SelectedPhoto == null)
                throw new InvalidOperationException("Photos are not selected");

            SelectedPhoto.FilePath.OpenFileInExplorer();
        }

        private void OpenDirectoryInExplorer()
        {
            _logger.Debug($"Opening current directory ({CurrentDirectoryPath}) in explorer...");
            if (CurrentDirectoryPath == null)
                throw new InvalidOperationException("No directory entered");

            CurrentDirectoryPath.OpenDirectoryInExplorer();
        }

        private void OpenPhoto()
        {
            _logger.Debug($"Opening selected photo ({SelectedPhoto?.FilePath}) in a separate window...");
            if (SelectedPhoto == null)
                throw new InvalidOperationException("Photos are not selected");

            ReselectPhoto();
            var mainWindow = _lifetimeScope.Resolve<WindowFactory<IMainWindow>>().GetWindow();
            var photoViewModel = _lifetimeScope.Resolve<PhotoViewModel>(
                new TypedParameter(typeof(MainViewModel), this),
                new TypedParameter(typeof(Photo), SelectedPhoto)
            );
            //Window is shown in its constructor
            var window = _lifetimeScope.Resolve<IPhotoWindow>(
                new TypedParameter(typeof(Window), mainWindow),
                new TypedParameter(typeof(PhotoViewModel), photoViewModel)
            );
            _windowsArranger.Add(window);
        }

        private void SelectionChanged([CanBeNull] IList items)
        {
            SelectedPhotos = items?.Cast<Photo>() ?? throw new ArgumentNullException(nameof(items));
            SelectedCount = SelectedPhotos.Count();
            SelectedPhoto?.ReloadCollectionInfoIfNeeded();
        }

        private void WindowClosing()
        {
            _logger.Debug("Performing cleanup before dependencies disposal...");
            //Need to finish current task before disposal (especially, repository)
            PhotoCollection.CancelCurrentTasks();
        }

        private void OpenSettingsFolder()
        {
            _logger.Debug($"Opening setting folder ({Paths.SettingsPath})...");
            $@"{Paths.SettingsPath}".OpenDirectoryInExplorer();
        }

        private void ViewLogs()
        {
            var logsPath =
                $@"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}\{nameof(Scar)}\{
                        nameof(PhotoReviewer)
                    }\Logs\Full.log";
            _logger.Debug($"Viewing logs file ({logsPath})...");
            logsPath.OpenFile();
        }

        #endregion
    }
}