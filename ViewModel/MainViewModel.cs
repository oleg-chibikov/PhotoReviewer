using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Shell;
using Microsoft.Extensions.Logging;
using PhotoReviewer.Contracts.DAL;
using PhotoReviewer.Contracts.View;
using PhotoReviewer.Core;
using PropertyChanged;
using Scar.Common.ApplicationLifetime.Contracts;
using Scar.Common.Events;
using Scar.Common.IO;
using Scar.Common.MVVM.Commands;
using Scar.Common.View.WindowCreation;

namespace PhotoReviewer.ViewModel
{
    // TODO: More logs
    // TODO: Localization
    // TODO: Detect photo change (rotate in standard viewer)
    [AddINotifyPropertyChangedInterface]

    public partial class MainViewModel : IDisposable
    {
        readonly Func<MainViewModel, Photo, PhotoViewModel> _photoViewModelFactory;

        readonly Func<IMainWindow, PhotoViewModel, IPhotoWindow> _photoWindowFactory;

        readonly WindowFactory<IMainWindow> _mainWindowFactory;

        readonly ILogger _logger;

        readonly ISettingsRepository _settingsRepository;

        readonly SynchronizationContext _syncContext = SynchronizationContext.Current ?? throw new InvalidOperationException("SynchronizationContext.Current is null");

        readonly WindowsArranger _windowsArranger;

        readonly IAssemblyInfoProvider _assemblyInfoProvider;

        public MainViewModel(
            PhotoCollection photoCollection,
            ISettingsRepository settingsRepository,
            WindowsArranger windowsArranger,
            ILogger<MainViewModel> logger,
            Func<MainViewModel, ShiftDateViewModel> shiftDateViewModelFactory,
            WindowFactory<IMainWindow> mainWindowFactory,
            Func<MainViewModel, Photo, PhotoViewModel> photoViewModelFactory,
            Func<IMainWindow, PhotoViewModel, IPhotoWindow> photoWindowFactory,
            ICommandManager commandManager,
            IAssemblyInfoProvider assemblyInfoProvider)
        {
            _ = commandManager ?? throw new ArgumentNullException(nameof(commandManager));
            PhotoCollection = photoCollection ?? throw new ArgumentNullException(nameof(photoCollection));
            _settingsRepository = settingsRepository ?? throw new ArgumentNullException(nameof(settingsRepository));
            _windowsArranger = windowsArranger;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            shiftDateViewModelFactory = shiftDateViewModelFactory ?? throw new ArgumentNullException(nameof(shiftDateViewModelFactory));
            _mainWindowFactory = mainWindowFactory ?? throw new ArgumentNullException(nameof(mainWindowFactory));
            _photoViewModelFactory = photoViewModelFactory ?? throw new ArgumentNullException(nameof(photoViewModelFactory));
            _photoWindowFactory = photoWindowFactory ?? throw new ArgumentNullException(nameof(photoWindowFactory));
            _assemblyInfoProvider = assemblyInfoProvider ?? throw new ArgumentNullException(nameof(assemblyInfoProvider));

            BrowseDirectoryCommand = new CorrelationCommand(commandManager, BrowseDirectory);
            ChangeDirectoryPathCommand = new CorrelationCommand<string>(commandManager, ChangeDirectoryPath);
            ShowOnlyMarkedChangedCommand = new CorrelationCommand<bool>(commandManager, ShowOnlyMarkedChanged);
            CopyFavoritedCommand = new CorrelationCommand(commandManager, CopyFavoritedAsync);
            DeleteMarkedCommand = new CorrelationCommand(commandManager, DeleteMarkedAsync);
            FavoriteCommand = new CorrelationCommand(commandManager, Favorite);
            MarkForDeletionCommand = new CorrelationCommand(commandManager, MarkForDeletion);
            RenameToDateCommand = new CorrelationCommand(commandManager, RenameToDateAsync);
            OpenPhotoInExplorerCommand = new CorrelationCommand(commandManager, OpenPhotoInExplorer);
            OpenDirectoryInExplorerCommand = new CorrelationCommand(commandManager, OpenDirectoryInExplorer);
            OpenPhotoCommand = new CorrelationCommand(commandManager, OpenPhotoAsync);
            SelectionChangedCommand = new CorrelationCommand<IList>(commandManager, SelectionChanged);
            WindowClosingCommand = new CorrelationCommand(commandManager, WindowClosing);
            OpenSettingsFolderCommand = new CorrelationCommand(commandManager, OpenSettingsFolder);
            ViewLogsCommand = new CorrelationCommand(commandManager, ViewLogs);
            PhotoCollection.Progress += PhotosCollection_Progress;
            PhotoCollection.CollectionChanged += PhotoCollection_CollectionChanged;
            PhotoCollection.PhotoNotification += PhotoCollection_PhotoNotification;
            ShiftDateViewModel = shiftDateViewModelFactory(this);

            var directoryPath = settingsRepository.Settings.LastUsedDirectoryPath;
            if (!string.IsNullOrWhiteSpace(directoryPath) && Directory.Exists(directoryPath))
            {
                SetDirectoryPathAsync(directoryPath);
            }
        }

        [DoNotNotify]
        public PhotoCollection PhotoCollection { get; }

        [DoNotNotify]
        public ShiftDateViewModel ShiftDateViewModel { get; }

        public double PhotoSize { get; set; } = 230;

        public Photo? SelectedPhoto { get; set; }

        public int SelectedCount { get; set; }

        public int Progress { get; set; }

        public string ProgressDescription { get; set; } = string.Empty;

        public TaskbarItemProgressState ProgressState { get; set; }

        public string? CurrentDirectoryPath { get; set; }

        public ICommand BrowseDirectoryCommand { get; }

        public ICommand ChangeDirectoryPathCommand { get; }

        public ICommand ShowOnlyMarkedChangedCommand { get; }

        public ICommand CopyFavoritedCommand { get; }

        public ICommand DeleteMarkedCommand { get; }

        public ICommand FavoriteCommand { get; }

        public ICommand MarkForDeletionCommand { get; }

        public ICommand OpenPhotoInExplorerCommand { get; }

        public ICommand OpenDirectoryInExplorerCommand { get; }

        public ICommand RenameToDateCommand { get; }

        public ICommand OpenPhotoCommand { get; }

        public ICommand SelectionChangedCommand { get; }

        public ICommand WindowClosingCommand { get; }

        public ICommand ViewLogsCommand { get; }

        public ICommand OpenSettingsFolderCommand { get; }

        [DoNotNotify]
        internal IEnumerable<Photo> SelectedPhotos { get; set; } = Array.Empty<Photo>();

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        internal void Favorite()
        {
            _logger.LogInformation("(Un)Favoriting selected photos...");
            if (!SelectedPhotos.Any())
            {
                throw new InvalidOperationException("Photos are not selected");
            }

            PhotoCollection.Favorite(SelectedPhotos.ToArray());
        }

        internal void MarkForDeletion()
        {
            _logger.LogInformation("(Un)Marking selected photos for deletion...");
            if (!SelectedPhotos.Any())
            {
                throw new InvalidOperationException("Photos are not selected");
            }

            PhotoCollection.MarkForDeletion(SelectedPhotos.ToArray());
        }

        internal async void RenameToDateAsync()
        {
            _logger.LogInformation("Renaming selected photos to date...");
            if (!SelectedPhotos.Any())
            {
                throw new InvalidOperationException("Photos are not selected");
            }

            await PhotoCollection.RenameToDateAsync(SelectedPhotos.ToArray()).ConfigureAwait(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                PhotoCollection.Progress -= PhotosCollection_Progress;
                PhotoCollection.PhotoNotification -= PhotoCollection_PhotoNotification;
                PhotoCollection.CollectionChanged -= PhotoCollection_CollectionChanged;
                _mainWindowFactory.Dispose();
                PhotoCollection.Dispose();
            }
        }

        void BeginProgress()
        {
            ProgressState = TaskbarItemProgressState.Normal;
            ProgressDescription = "Caclulating...";
            Progress = 0;
        }

        void EndProgress()
        {
            ProgressState = TaskbarItemProgressState.None;
        }

        void PhotoCollection_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (SelectedPhoto == null && e.NewItems != null)
                    {
                        SelectedPhoto = e.NewItems.Cast<Photo>().First();

                        // SelectionChanged is not hit sometimes
                        SelectionChanged(e.NewItems);
                    }

                    break;
                case NotifyCollectionChangedAction.Remove:
                    if (e.OldItems != null)
                    {
                        foreach (var photo in e.OldItems.Cast<Photo>())
                        {
                            _windowsArranger.ClosePhoto(photo);
                        }
                    }

                    break;
            }
        }

        void PhotoCollection_PhotoNotification(object? sender, EventArgs e)
        {
            foreach (var photo in _windowsArranger.Photos)
            {
                photo.ReloadCollectionInfoIfNeeded();
            }

            SelectedPhoto?.ReloadCollectionInfoIfNeeded();
        }

        void PhotosCollection_Progress(object? sender, ProgressEventArgs e)
        {
            _syncContext.Send(
                x =>
                {
                    Progress = e.Percentage;
                    ProgressDescription = $"{e.Current} of {e.Total} ({e.Percentage} %)";
                    if (e.Current == 0)
                    {
                        BeginProgress();
                    }
                    else if (e.Current == e.Total)
                    {
                        EndProgress();
                    }
                },
                null);
        }

        /// <summary>Selects and scrolls to the first photo of current selection.</summary>
        void ReselectPhoto()
        {
            var photo = SelectedPhoto;
            SelectedPhoto = null;
            SelectedPhoto = photo;
        }

        async void SetDirectoryPathAsync(string directoryPath)
        {
            directoryPath = directoryPath.RemoveTrailingBackslash();
            var settings = _settingsRepository.Settings;
            var task = PhotoCollection.SetDirectoryPathAsync(directoryPath);

            if (task.IsCompleted || task.IsFaulted)
            {
                // Restore previous path since current is corrupted
                CurrentDirectoryPath = null;
                CurrentDirectoryPath = settings.LastUsedDirectoryPath;
            }
            else
            {
                _windowsArranger.ClosePhotos();
                settings.LastUsedDirectoryPath = CurrentDirectoryPath = directoryPath;
                _settingsRepository.Settings = settings;
            }

            await task.ConfigureAwait(true);
        }

        void BrowseDirectory()
        {
            _logger.LogTrace("Browsing directory...");

            // TODO: Another dialog third party? Use OpenFileService and DI
            using var dialog = new FolderBrowserDialog();
            var lastUsedPath = _settingsRepository.Settings.LastUsedDirectoryPath;

            if (!string.IsNullOrEmpty(lastUsedPath))
            {
                dialog.SelectedPath = lastUsedPath;
            }

            if (dialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            _logger.LogDebug($"Changing directory path to {dialog.SelectedPath}...");
            SetDirectoryPathAsync(dialog.SelectedPath);
        }

        void ChangeDirectoryPath(string directoryPath)
        {
            _logger.LogDebug($"Changing directory path to {directoryPath}...");
            if (directoryPath == null)
            {
                throw new ArgumentNullException(nameof(directoryPath));
            }

            SetDirectoryPathAsync(directoryPath);
        }

        void ShowOnlyMarkedChanged(bool isChecked)
        {
            _logger.LogDebug($"Setting show only marked to {isChecked}...");
            PhotoCollection.ChangeFilter(isChecked);
        }

        async void CopyFavoritedAsync()
        {
            _logger.LogInformation("Copying favorited photos...");
            await PhotoCollection.CopyFavoritedAsync().ConfigureAwait(true);
        }

        async void DeleteMarkedAsync()
        {
            _logger.LogInformation("Deleting marked photos...");
            await PhotoCollection.DeleteMarkedAsync().ConfigureAwait(true);
        }

        void OpenPhotoInExplorer()
        {
            _logger.LogTrace($"Opening selected photo ({SelectedPhoto?.FileLocation}) in explorer...");
            if (SelectedPhoto == null)
            {
                throw new InvalidOperationException("Photos are not selected");
            }

            SelectedPhoto.FileLocation.ToString().OpenFileInExplorer();
        }

        void OpenDirectoryInExplorer()
        {
            _logger.LogTrace($"Opening current directory ({CurrentDirectoryPath}) in explorer...");
            if (CurrentDirectoryPath == null)
            {
                throw new InvalidOperationException("No directory entered");
            }

            CurrentDirectoryPath.OpenDirectoryInExplorer();
        }

        async void OpenPhotoAsync()
        {
            _logger.LogTrace($"Opening selected photo ({SelectedPhoto?.FileLocation}) in a separate window...");
            if (SelectedPhoto == null)
            {
                throw new InvalidOperationException("Photos are not selected");
            }

            ReselectPhoto();
            var mainWindow = await _mainWindowFactory.GetWindowAsync(CancellationToken.None).ConfigureAwait(false);
            var photoViewModel = _photoViewModelFactory(this, SelectedPhoto);

            // Window is shown in its constructor
            var window = _photoWindowFactory(mainWindow, photoViewModel);
            _windowsArranger.Add(window);
        }

        void SelectionChanged(IList items)
        {
            SelectedPhotos = items?.Cast<Photo>() ?? throw new ArgumentNullException(nameof(items));
            SelectedCount = SelectedPhotos.Count();
            SelectedPhoto?.ReloadCollectionInfoIfNeeded();
        }

        void WindowClosing()
        {
            _logger.LogTrace("Performing cleanup before dependencies disposal...");

            // Need to finish current task before disposal (especially, repository)
            PhotoCollection.CancelCurrentTasks();
        }

        void OpenSettingsFolder()
        {
            _logger.LogTrace($"Opening setting folder ({_assemblyInfoProvider.SettingsPath})...");
            $@"{_assemblyInfoProvider.SettingsPath}".OpenDirectoryInExplorer();
        }

        void ViewLogs()
        {
            var logsPath = PathsProvider.LogsPath;
            _logger.LogTrace($"Viewing logs file ({logsPath})...");
            logsPath.OpenFile();
        }
    }
}
