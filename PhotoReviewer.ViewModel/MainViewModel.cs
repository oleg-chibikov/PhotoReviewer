using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Shell;
using Autofac;
using Common.Logging;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using GalaSoft.MvvmLight.Threading;
using JetBrains.Annotations;
using PhotoReviewer.Core;
using PhotoReviewer.DAL.Contracts;
using PhotoReviewer.DAL.Contracts.Model;
using PhotoReviewer.Resources;
using PhotoReviewer.View.Contracts;
using PropertyChanged;
using Scar.Common.IO;
using Scar.Common.WPF;

namespace PhotoReviewer.ViewModel
{
    //TODO: More logs
    //TODO: Localization
    //TODO: CorrelationId for all actions

    [ImplementPropertyChanged]
    public class MainViewModel: IDisposable
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

        [NotNull]
        private readonly IMessenger messenger;

        public MainViewModel([NotNull] IMessenger messenger, [NotNull] PhotoCollection photoCollection, [NotNull] ISettingsRepository settingsRepository, [NotNull] ILifetimeScope lifetimeScope, [NotNull] WindowsArranger windowsArranger, [NotNull] ILog logger)
        {
            if (messenger == null)
                throw new ArgumentNullException(nameof(messenger));
            if (photoCollection == null)
                throw new ArgumentNullException(nameof(photoCollection));
            if (settingsRepository == null)
                throw new ArgumentNullException(nameof(settingsRepository));
            if (lifetimeScope == null)
                throw new ArgumentNullException(nameof(lifetimeScope));
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            PhotoCollection = photoCollection;
            this.messenger = messenger;
            this.settingsRepository = settingsRepository;
            this.lifetimeScope = lifetimeScope;
            this.windowsArranger = windowsArranger;
            this.logger = logger;
            PhotoCollection.Progress += PhotosCollection_Progress;
            PhotoCollection.CollectionChanged += PhotoCollection_CollectionChanged;

            var directoryPath = settingsRepository.Get().LastUsedDirectoryPath;
            if (!string.IsNullOrWhiteSpace(directoryPath) && Directory.Exists(directoryPath))
                SetNewDirectoryPath(directoryPath);
            BrowseDirectoryCommand = new RelayCommand(BrowseDirectory);
            ChangeDirectoryPathCommand = new RelayCommand<string>(ChangeDirectoryPath);
            ShowOnlyMarkedChangedCommand = new RelayCommand<bool>(ShowOnlyMarkedChanged);
            CopyFavoritedCommand = new RelayCommand(CopyFavorited);
            DeleteMarkedCommand = new RelayCommand(DeleteMarked);
            FavoriteCommand = new RelayCommand(Favorite);
            MarkForDeletionCommand = new RelayCommand(MarkForDeletion);
            RenameToDateCommand = new RelayCommand(RenameToDate);
            OpenPhotoInExplorerCommand = new RelayCommand(OpenPhotoInExplorer);
            OpenDirectoryInExplorerCommand = new RelayCommand(OpenDirectoryInExplorer);
            OpenPhotoCommand = new RelayCommand(OpenPhoto);
            SelectionChangedCommand = new RelayCommand<IList>(SelectionChanged);
            WindowClosingCommand = new RelayCommand(WindowClosing);
            OpenSettingsFolderCommand = new RelayCommand(OpenSettingsFolder);
            ViewLogsCommand = new RelayCommand(ViewLogs);
        }

        [NotNull]
        public PhotoCollection PhotoCollection { get; }

        private void PhotoCollection_CollectionChanged([NotNull] object sender, [NotNull] NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (SelectedPhoto == null)
                        SelectedPhoto = e.NewItems.Cast<Photo>().First();
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

        private void SetNewDirectoryPath([NotNull] string directoryPath)
        {
            var settings = settingsRepository.Get();
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                RestoreCurrentPath(settings, Errors.SelectDirectory);
                return;
            }
            if (!Directory.Exists(directoryPath))
            {
                RestoreCurrentPath(settings, string.Format(Errors.DirectoryDoesNotExist, directoryPath));
                return;
            }
            windowsArranger.ClosePhotos();
            var task = PhotoCollection.SetDirectoryPathAsync(directoryPath);
            if (task.IsCompleted)
                return;
            BeginProgress();
            settings.LastUsedDirectoryPath = CurrentPath = directoryPath;
            settingsRepository.Save(settings);
        }

        private void RestoreCurrentPath([NotNull] Settings settings, [NotNull] string message)
        {
            messenger.Send(message, MessengerTokens.UserWarningToken);
            CurrentPath = null;
            CurrentPath = settings.LastUsedDirectoryPath;
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
            //TODO: Another dialog third party? Use OpenFileService and DI
            var dialog = new FolderBrowserDialog();
            var lastUsedPath = settingsRepository.Get().LastUsedDirectoryPath;

            if (!string.IsNullOrEmpty(lastUsedPath))
                dialog.SelectedPath = lastUsedPath;
            if (dialog.ShowDialog() != DialogResult.OK)
                return;
            SetNewDirectoryPath(dialog.SelectedPath);
        }

        private void ChangeDirectoryPath([NotNull] string directoryPath)
        {
            if (directoryPath == null)
                throw new ArgumentNullException(nameof(directoryPath));
            SetNewDirectoryPath(directoryPath);
        }

        private void ShowOnlyMarkedChanged(bool isChecked)
        {
            PhotoCollection.ShowOnlyMarked = isChecked;
        }

        private void CopyFavorited()
        {
            var task = PhotoCollection.CopyFavoritedAsync();
            if (!task.IsCompleted)
                BeginProgress();
        }

        private void DeleteMarked()
        {
            var task = PhotoCollection.DeleteMarkedAsync();
            if (!task.IsCompleted)
                BeginProgress();
        }

        public void Favorite()
        {
            if (!selectedPhotos.Any())
                throw new InvalidOperationException("No photo selected");
            PhotoCollection.Favorite(selectedPhotos.ToArray());
        }

        public void MarkForDeletion()
        {
            if (!selectedPhotos.Any())
                throw new InvalidOperationException("No photo selected");
            PhotoCollection.MarkForDeletion(selectedPhotos.ToArray());
        }

        public void RenameToDate()
        {
            if (!selectedPhotos.Any())
                throw new InvalidOperationException("No photo selected");
            var task = PhotoCollection.RenameToDateAsync(selectedPhotos.ToArray());
            if (!task.IsCompleted)
                BeginProgress();
        }

        private void OpenPhotoInExplorer()
        {
            if (SelectedPhoto == null)
                throw new InvalidOperationException("No photo selected");
            DirectoryUtility.OpenFileInExplorer(SelectedPhoto.FilePath);
        }

        private void OpenDirectoryInExplorer()
        {
            if (CurrentPath == null)
                throw new InvalidOperationException("No directory entered");
            DirectoryUtility.OpenDirectoryInExplorer(CurrentPath);
        }

        private void OpenPhoto()
        {
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

        /// <summary>
        /// Selects and scrolls to the first photo of current selection
        /// </summary>
        private void ReselectPhoto()
        {
            var photo = SelectedPhoto;
            SelectedPhoto = null;
            SelectedPhoto = photo;
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
            //Need to finish current task before disposal (especially, repository)
            PhotoCollection.CancelCurrentTask();
        }
        private static void OpenSettingsFolder()
        {
            Trace.CorrelationManager.ActivityId = Guid.NewGuid();
            DirectoryUtility.OpenDirectoryInExplorer($@"{Paths.SettingsPath}");
        }

        private static void ViewLogs()
        {
            Trace.CorrelationManager.ActivityId = Guid.NewGuid();
            DirectoryUtility.OpenFile($@"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}\{nameof(Scar)}\{nameof(PhotoReviewer)}\Logs\Full.log");
        }

        #endregion

        public void Dispose()
        {
            PhotoCollection.Progress -= PhotosCollection_Progress;
            PhotoCollection.CollectionChanged -= PhotoCollection_CollectionChanged;
        }
    }
}