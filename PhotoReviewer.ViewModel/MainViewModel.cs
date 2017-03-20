using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Shell;
using Autofac;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using JetBrains.Annotations;
using PhotoReviewer.Core;
using PhotoReviewer.DAL.Contracts;
using PhotoReviewer.Resources;
using PhotoReviewer.View.Contracts;
using Scar.Common.IO;
using Scar.Common.WPF;

namespace PhotoReviewer.ViewModel
{
    //TODO: More logs
    //TODO: Clear DB on startup (background) - if any photo does not exist anymore - delete from db
    //TODO: If there is favorites folder under current - check every file from it and make original marked as favorite (process in background)

    //TODO: Internal
    public class MainViewModel : ViewModelBase, IRequestCloseViewModel
    {
        //TODO: Dispose and unbind events
        [NotNull]
        private readonly FileSystemWatcher imagesDirectoryWatcher = new FileSystemWatcher
        {
            //TODO: polling every n seconds or use queue for handlers
            InternalBufferSize = 64 * 1024
        };

        [NotNull]
        private readonly ILifetimeScope lifetimeScope;

        [NotNull]
        private readonly ISettingsRepository settingsRepository;

        [NotNull]
        private readonly WindowsArranger windowsArranger;

        [NotNull]
        private readonly TaskFactory uiFactory;

        private IEnumerable<Photo> selectedItems;

        public MainViewModel([NotNull] IMessenger messenger, [NotNull] PhotoCollection photoCollection, [NotNull] ISettingsRepository settingsRepository, [NotNull] TaskFactory uiFactory, [NotNull] ILifetimeScope lifetimeScope, [NotNull] WindowsArranger windowsArranger) : base(messenger)
        {
            if (messenger == null)
                throw new ArgumentNullException(nameof(messenger));
            if (photoCollection == null)
                throw new ArgumentNullException(nameof(photoCollection));
            if (settingsRepository == null)
                throw new ArgumentNullException(nameof(settingsRepository));
            if (uiFactory == null)
                throw new ArgumentNullException(nameof(uiFactory));
            if (lifetimeScope == null)
                throw new ArgumentNullException(nameof(lifetimeScope));
            PhotoCollection = photoCollection;
            this.settingsRepository = settingsRepository;
            this.uiFactory = uiFactory;
            this.lifetimeScope = lifetimeScope;
            this.windowsArranger = windowsArranger;
            //TODO: IDisposable
            imagesDirectoryWatcher.Created += ImagesDirectoryWatcher_Changed;
            imagesDirectoryWatcher.Deleted += ImagesDirectoryWatcher_Changed;
            imagesDirectoryWatcher.Renamed += ImagesDirectoryWatcher_Renamed;
            PhotoCollection.PhotoDeleted += PhotoCollection_PhotoDeleted;
            PhotoCollection.Progress += PhotosCollection_Progress;

            var path = settingsRepository.Get().LastUsedPath;
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                SetNewPath(path);
            BrowseDirectoryCommand = new RelayCommand(BrowseDirectory);
            ChangePathCommand = new RelayCommand<string>(ChangePath);
            ShowOnlyMarkedChangedCommand = new RelayCommand<bool>(ShowOnlyMarkedChanged);
            MoveFavoritedCommand = new RelayCommand(MoveFavorited);
            DeleteMarkedCommand = new RelayCommand(DeleteMarked);
            FavoriteCommand = new RelayCommand(Favorite);
            MarkForDeletionCommand = new RelayCommand(MarkForDeletion);
            RenameToDateCommand = new RelayCommand(RenameToDate);
            OpenPhotoInExplorerCommand = new RelayCommand(OpenPhotoInExplorer);
            OpenDirectoryInExplorerCommand = new RelayCommand(OpenDirectoryInExplorer);
            OpenPhotoCommand = new RelayCommand(OpenPhoto);
            SelectionChangedCommand = new RelayCommand<IList>(SelectionChanged);
            WindowClosingCommand = new RelayCommand<CancelEventArgs>(WindowClosing);
        }
        
        public event EventHandler RequestClose;

        private void PhotoCollection_PhotoDeleted(object sender, PhotoDeletedEventArgs e)
        {
            uiFactory.StartNew(() => { windowsArranger.ClosePhotos(e.Path); });
        }

        private void PhotosCollection_Progress(object sender, ProgressEventArgs e)
        {
            uiFactory.StartNew(() =>
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

        private void ImagesDirectoryWatcher_Changed([NotNull] object sender, [NotNull] FileSystemEventArgs fileSystemEventArgs)
        {
            var path = fileSystemEventArgs.FullPath;
            if (!IsImage(path))
                return;

            uiFactory.StartNew(() =>
            {
                switch (fileSystemEventArgs.ChangeType)
                {
                    case WatcherChangeTypes.Deleted:
                        PhotoCollection.DeletePhotoAsync(path);
                        break;
                    case WatcherChangeTypes.Created:
                        PhotoCollection.GetDetailsAndAddPhotoAsync(path);
                        break;
                }
            });
        }

        private static bool IsImage(string path)
        {
            var extenstion = Path.GetExtension(path);
            return extenstion != null && Constants.FileExtensions.Contains(extenstion, StringComparer.InvariantCultureIgnoreCase);
        }

        private void ImagesDirectoryWatcher_Renamed([NotNull] object sender, [NotNull] RenamedEventArgs renamedEventArgs)
        {
            if (!IsImage(renamedEventArgs.FullPath))
                return;
            uiFactory.StartNew(() => { PhotoCollection.RenamePhotoAsync(renamedEventArgs.OldFullPath, renamedEventArgs.FullPath); });
        }

        private void SetNewPath([NotNull] string path)
        {
            var settings = settingsRepository.Get();
            if (!Directory.Exists(path))
            {
                MessengerInstance.Send(string.Format(Errors.DirecoryDoesNotExist, path), MessengerTokens.UserWarningToken);
                CurrentPath = settings.LastUsedPath;
                return;
            }
            windowsArranger.ClosePhotos();
            imagesDirectoryWatcher.EnableRaisingEvents = false;
            var task = PhotoCollection.SetPathAsync(path);
            if (task.IsCompleted)
                return;
            BeginProgress();
            imagesDirectoryWatcher.Path = settings.LastUsedPath = CurrentPath = path;
            imagesDirectoryWatcher.EnableRaisingEvents = true;
            settingsRepository.Save(settings);
            //TODO: select first photo only when it is loaded (maybe event from photocollection)
        }

        #region Dependency Properties
        //TODO: Annotations
        private int progress;

        public int Progress
        {
            get { return progress; }
            set { Set(() => Progress, ref progress, value); }
        }

        private TaskbarItemProgressState progressState;

        public TaskbarItemProgressState ProgressState
        {
            get { return progressState; }
            set { Set(() => ProgressState, ref progressState, value); }
        }

        private string currentPath;

        public string CurrentPath
        {
            get { return currentPath; }
            set { Set(() => CurrentPath, ref currentPath, value); }
        }

        private Photo selectedPhoto;

        public Photo SelectedPhoto
        {
            get { return selectedPhoto; }
            set { Set(() => SelectedPhoto, ref selectedPhoto, value); }
        }

        [NotNull]
        public PhotoCollection PhotoCollection { get; }

        #endregion

        #region Commands

        [NotNull]
        public ICommand BrowseDirectoryCommand { get; }
        [NotNull]
        public ICommand ChangePathCommand { get; }
        [NotNull]
        public ICommand ShowOnlyMarkedChangedCommand { get; }
        [NotNull]
        public ICommand MoveFavoritedCommand { get; }
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

        #endregion

        #region Command handlers

        private void BrowseDirectory()
        {
            //TODO: Another dialog third party? Use OpenFileService and DI
            var dialog = new FolderBrowserDialog();
            var lastUsedPath = settingsRepository.Get().LastUsedPath;

            if (!string.IsNullOrEmpty(lastUsedPath))
                dialog.SelectedPath = lastUsedPath;
            if (dialog.ShowDialog() != DialogResult.OK)
                return;
            SetNewPath(dialog.SelectedPath);
        }

        private void ChangePath([NotNull] string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            SetNewPath(path);
        }

        private void ShowOnlyMarkedChanged(bool isChecked)
        {
            PhotoCollection.ShowOnlyMarked = isChecked;
        }

        private void MoveFavorited()
        {
            var task = PhotoCollection.MoveFavoritedAsync();
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
            PhotoCollection.Favorite(selectedItems.ToArray());
        }

        public void MarkForDeletion()
        {
            PhotoCollection.MarkForDeletion(selectedItems.ToArray());
        }

        public void RenameToDate()
        {
            var task = PhotoCollection.RenameToDateAsync(selectedItems.ToArray());
            if (!task.IsCompleted)
                BeginProgress();
        }

        private void OpenPhotoInExplorer()
        {
            DirectoryUtility.OpenFileInExplorer(SelectedPhoto.FilePath);
        }

        private void OpenDirectoryInExplorer()
        {
            DirectoryUtility.OpenDirectoryInExplorer(CurrentPath);
        }

        private void OpenPhoto()
        {
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
            selectedItems = items.Cast<Photo>();
        }

        private void WindowClosing([NotNull] CancelEventArgs args)
        {
            if (args == null)
                throw new ArgumentNullException(nameof(args));
            //Need to finish current task before disposal (especially, repository)
            PhotoCollection.CancelCurrentTask();
        }

        #endregion
    }
}