using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using Autofac;
using Common.Logging;
using GalaSoft.MvvmLight.Messaging;
using GalaSoft.MvvmLight.Threading;
using JetBrains.Annotations;
using PhotoReviewer.DAL.Contracts;
using PhotoReviewer.Resources;
using Scar.Common;
using Scar.Common.Drawing;
using Scar.Common.IO;

//TODO: BlockingCollection for photos commands

namespace PhotoReviewer.ViewModel
{
    /// <summary>
    /// This class represents a collection of photos in a directory.
    /// </summary>
    [UsedImplicitly]
    public sealed class PhotoCollection : ObservableCollection<Photo>, IDisposable
    {
        [NotNull]
        private readonly IComparer<string> comparer;


        [NotNull]
        private readonly FileSystemWatcher imagesDirectoryWatcher = new FileSystemWatcher
        {
            //TODO: polling every n seconds or use queue for handlers
            InternalBufferSize = 64 * 1024
        };

        [NotNull]
        private readonly ILifetimeScope lifetimeScope;

        [NotNull]
        private readonly ILog logger;

        [NotNull]
        private readonly IMessenger messenger;

        [NotNull]
        private readonly IMetadataExtractor metadataExtractor;

        [NotNull]
        private readonly IPhotoUserInfoRepository repository;

        [NotNull]
        private readonly Predicate<object> showOnlyMarkedFilter = x => ((Photo)x).IsValuableOrNearby;

        [NotNull]
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        [NotNull]
        private Task currentTask = Task.CompletedTask;

        private bool showOnlyMarked;

        public PhotoCollection([NotNull] IComparer<string> comparer,
            [NotNull] IMessenger messenger,
            [NotNull] ILog logger,
            [NotNull] IMetadataExtractor metadataExtractor,
            [NotNull] ILifetimeScope lifetimeScope,
            [NotNull] IPhotoUserInfoRepository repository)
        {
            if (comparer == null)
                throw new ArgumentNullException(nameof(comparer));
            if (messenger == null)
                throw new ArgumentNullException(nameof(messenger));
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            if (metadataExtractor == null)
                throw new ArgumentNullException(nameof(metadataExtractor));
            if (lifetimeScope == null)
                throw new ArgumentNullException(nameof(lifetimeScope));
            if (repository == null)
                throw new ArgumentNullException(nameof(repository));
            this.comparer = comparer;
            this.messenger = messenger;
            this.logger = logger;
            this.metadataExtractor = metadataExtractor;
            this.lifetimeScope = lifetimeScope;
            this.repository = repository;
            CollectionChanged += PhotoCollection_CollectionChanged;
            FilteredView = CollectionViewSource.GetDefaultView(this);
            imagesDirectoryWatcher.Created += ImagesDirectoryWatcher_Changed;
            imagesDirectoryWatcher.Deleted += ImagesDirectoryWatcher_Changed;
            imagesDirectoryWatcher.Renamed += ImagesDirectoryWatcher_Renamed;
        }

        [NotNull]
        public ICollectionView FilteredView { get; }

        public int FavoritedCount { get; set; }

        public int MarkedForDeletionCount { get; set; }

        public bool ShowOnlyMarked
        {
            get { return showOnlyMarked; }
            set
            {
                showOnlyMarked = value;
                FilteredView.Filter = !value ? null : showOnlyMarkedFilter;
            }
        }

        public event EventHandler<ProgressEventArgs> Progress;

        public void CancelCurrentTask()
        {
            cancellationTokenSource.Cancel();
        }

        #region Async operations

        [NotNull]
        public async Task SetDirectoryPathAsync([NotNull] string directoryPath)
        {
            if (directoryPath == null)
                throw new ArgumentNullException(nameof(directoryPath));
            logger.Info($"Changing directory path to '{directoryPath}'...");
            imagesDirectoryWatcher.EnableRaisingEvents = false;
            imagesDirectoryWatcher.Path = directoryPath;
            imagesDirectoryWatcher.EnableRaisingEvents = true;
            var token = RecreateCancellationToken();
            Clear();
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                messenger.Send(Errors.SelectDirectory, MessengerTokens.UserWarningToken);
                return;
            }
            if (!Directory.Exists(directoryPath))
            {
                messenger.Send(string.Format(Errors.DirectoryDoesNotExist, directoryPath), MessengerTokens.UserWarningToken);
                return;
            }
            var context = SynchronizationContext.Current;
            FavoritedCount = MarkedForDeletionCount = 0;
            currentTask = Task.Run(() =>
            {
                var files = GetFilesFromDirectory(directoryPath);
                var totalCount = files.Count();
                logger.Debug($"There are {totalCount} files in this directory");
                if (files.Any())
                {
                    var i = 0;
                    var maxBlockSize = 10;
                    files.RunByBlocks(maxBlockSize, (block, blocksCount) =>
                    {
                        if (token.IsCancellationRequested)
                            return false;
                        logger.Debug($"Processing block {i++} ({block.Length} files)...");
                        var detailsBlock = block.Select(GetPhotoDetails).ToArray();
                        context.Send(t =>
                        {
                            foreach (var details in detailsBlock)
                            {
                                var photo = lifetimeScope.Resolve<Photo>(
                                    new TypedParameter(typeof(PhotoDetails), details),
                                    new TypedParameter(typeof(PhotoCollection), this),
                                    new TypedParameter(typeof(CancellationToken), token));
                                if (token.IsCancellationRequested)
                                    break;
                                Add(photo);
                            }
                            OnProgress(100 * i / blocksCount);
                        }, null);
                        return true;
                    });
                }
                else
                {
                    OnProgress(100);
                }
                GC.Collect();
            }, token);
            await currentTask;
            //In order to display the Prev photos of Marked ones there is the need to refresh filter after all photos are loaded
            if (showOnlyMarked)
                FilteredView.Refresh();
        }

        [NotNull]
        public async Task RenameToDateAsync([NotNull] Photo[] photos)
        {
            if (!photos.Any())
            {
                messenger.Send(Errors.NothingToRename, MessengerTokens.UserWarningToken);
                return;
            }
            if (!currentTask.IsCompleted)
            {
                messenger.Send(Errors.TaskInProgress, MessengerTokens.UserWarningToken);
                return;
            }
            var token = RecreateCancellationToken();
            var data = photos.Select(x => new { x.Name, x.FilePath, x.Metadata.DateImageTaken }).ToArray();
            currentTask = Task.Run(() =>
            {
                var i = 0;
                var count = data.Length;
                foreach (var item in data)
                {
                    RenameFileToDate(item.Name, item.FilePath, item.DateImageTaken);
                    OnProgress(100 * ++i / count);
                }
            }, token);
            await currentTask;
        }

        [NotNull]
        public async Task DeleteMarkedAsync()
        {
            if (!currentTask.IsCompleted)
            {
                messenger.Send(Errors.TaskInProgress, MessengerTokens.UserWarningToken);
                return;
            }
            if (!this.Any(x => x.MarkedForDeletion))
            {
                messenger.Send(Errors.NothingToDelete, MessengerTokens.UserWarningToken);
                return;
            }
            var token = RecreateCancellationToken();

            currentTask = Task.Run(() =>
            {
                var i = 0;
                var count = Count;
                Parallel.ForEach(this, photo =>
                {
                    photo.DeleteFileIfMarked();
                    OnProgress(100 * Interlocked.Increment(ref i) / count);
                });
            }, token);
            await currentTask;
        }

        [NotNull]
        public async Task CopyFavoritedAsync()
        {
            if (!currentTask.IsCompleted)
            {
                messenger.Send(Errors.TaskInProgress, MessengerTokens.UserWarningToken);
                return;
            }
            if (!this.Any(x => x.Favorited))
            {
                messenger.Send(Errors.NothingToMove, MessengerTokens.UserWarningToken);
                return;
            }
            var token = RecreateCancellationToken();
            currentTask = Task.Run(() =>
            {
                var favoriteDirectories = this.Select(x => PhotoDetails.GetFavoriteDirectory(x.FilePath)).Distinct().ToArray();
                //Create directories firstly
                foreach (var favoriteDirectory in favoriteDirectories)
                    if (!Directory.Exists(favoriteDirectory))
                        Directory.CreateDirectory(favoriteDirectory);
                var i = 0;
                var count = Count;
                Parallel.ForEach(this, photo =>
                {
                    photo.CopyFileIfFavorited();
                    OnProgress(100 * Interlocked.Increment(ref i) / count);
                });
                //open not more than 3 directories
                foreach (var favoriteDirectory in favoriteDirectories.Take(3))
                    Process.Start(favoriteDirectory);
            }, token);
            await currentTask;
        }

        #endregion

        #region Sync operations

        public void MarkForDeletion([NotNull] Photo[] photos)
        {
            if (!photos.Any())
            {
                messenger.Send(Errors.NothingToMark, MessengerTokens.UserWarningToken);
                return;
            }
            var notMarked = photos.Where(x => !x.MarkedForDeletion).ToArray();
            if (notMarked.Any())
            {
                foreach (var photo in notMarked)
                {
                    photo.MarkedForDeletion = true;
                    photo.Favorited = false;
                }
                var paths = notMarked.Select(x => x.FilePath).ToArray();
                repository.MarkForDeletion(paths);
            }
            else
            {
                foreach (var photo in photos)
                    photo.MarkedForDeletion = false;
                repository.UnMarkForDeletion(photos.Select(x => x.FilePath).ToArray());
            }
        }

        public void Favorite([NotNull] Photo[] photos)
        {
            if (!photos.Any())
            {
                messenger.Send(Errors.NothingToFavorite, MessengerTokens.UserWarningToken);
                return;
            }
            var notFavorited = photos.Where(x => !x.Favorited).ToArray();
            if (notFavorited.Any())
            {
                foreach (var photo in notFavorited)
                {
                    photo.Favorited = true;
                    photo.MarkedForDeletion = false;
                }
                var paths = notFavorited.Select(x => x.FilePath).ToArray();
                repository.Favorite(paths);
            }
            else
            {
                foreach (var photo in photos)
                    photo.Favorited = false;
                repository.UnFavorite(photos.Select(x => x.FilePath).ToArray());
            }
        }

        #endregion

        #region WatcherHandlers

        private async void GetDetailsAndAddPhotoAsync([NotNull] string filePath)
        {
            await currentTask;
            var details = GetPhotoDetails(filePath);
            var photo = lifetimeScope.Resolve<Photo>(
                new TypedParameter(typeof(PhotoDetails), details),
                new TypedParameter(typeof(PhotoCollection), this),
                new TypedParameter(typeof(CancellationToken), CancellationToken.None));
            InsertAtProperIndex(photo);
        }

        private async void DeletePhotoAsync([NotNull] string filePath)
        {
            await currentTask;
            repository.Delete(filePath);
            var photo = this.SingleOrDefault(x => x.FilePath == filePath);
            if (photo == null)
                return;
            Remove(photo);
            if (photo.MarkedForDeletion)
                MarkedForDeletionCount--;
            if (photo.Favorited)
                FavoritedCount--;
        }

        private async void RenamePhotoAsync([NotNull] string oldFilePath, [NotNull] string newFilePath)
        {
            await currentTask;
            repository.Rename(oldFilePath, newFilePath);
            var photo = this.SingleOrDefault(x => x.FilePath == oldFilePath);
            if (photo == null)
                return;
            Remove(photo);
            photo.FilePath = newFilePath;
            InsertAtProperIndex(photo);
        }

        #endregion

        #region Private

        private CancellationToken RecreateCancellationToken()
        {
            CancelCurrentTask();
            cancellationTokenSource.Dispose();
            cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;
            return token;
        }

        private IOrderedEnumerable<string> GetFilesFromDirectory(string directory)
        {
            var files = DirectoryUtility.GetFiles(directory, Constants.FilterExtensions)
                .OrderBy(f => f, comparer);
            return files;
        }

        private void RenameFileToDate([NotNull] string name, [NotNull] string filePath, [CanBeNull] DateTime? dateImageTaken)
        {
            var oldFilePath = filePath;
            if (!File.Exists(filePath) || !dateImageTaken.HasValue)
                return;
            var newName = dateImageTaken.Value.ToString("yyyy-MM-dd HH-mm-ss");
            if (newName == name)
                return;
            var directoryName = Path.GetDirectoryName(filePath);
            var extension = Path.GetExtension(filePath);
            var newFilePath = DirectoryUtility.GetFreeFileName($"{directoryName}\\{newName}{extension}");
            if (!File.Exists(newFilePath))
                File.Move(oldFilePath, newFilePath);
            //FileSystemWatcher will do the rest
        }

        private void OnProgress(int percent)
        {
            Progress?.Invoke(this, new ProgressEventArgs(percent));
        }

        private void PhotoCollection_CollectionChanged([NotNull] object sender, [NotNull] NotifyCollectionChangedEventArgs e)
        {
            foreach (var photo in this)
                photo.OnCollectionChanged();
        }

        private PhotoDetails GetPhotoDetails([NotNull] string filePath)
        {
            var metadata = metadataExtractor.Extract(filePath);
            var photoUserInfo = repository.Check(filePath);
            var details = new PhotoDetails(filePath, metadata, photoUserInfo.MarkedForDeletion, photoUserInfo.Favorited);
            if (!photoUserInfo.Favorited && details.Favorited)
                repository.Favorite(filePath);
            return details;
        }

        private void InsertAtProperIndex([NotNull] Photo photo)
        {
            //http://stackoverflow.com/questions/748596/finding-best-position-for-element-in-list
            var name = photo.Name;
            var index = Array.BinarySearch(this.Select(x => x.Name).ToArray(), name, comparer);
            var insertIndex = ~index;
            Insert(insertIndex, photo);
        }

        private static bool IsImage(string filePath)
        {
            var extenstion = Path.GetExtension(filePath);
            return extenstion != null && Constants.FileExtensions.Contains(extenstion, StringComparer.InvariantCultureIgnoreCase);
        }

        private void ImagesDirectoryWatcher_Changed([NotNull] object sender, [NotNull] FileSystemEventArgs fileSystemEventArgs)
        {
            var filePath = fileSystemEventArgs.FullPath;
            if (!IsImage(filePath))
                return;

            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                switch (fileSystemEventArgs.ChangeType)
                {
                    case WatcherChangeTypes.Deleted:
                        DeletePhotoAsync(filePath);
                        break;
                    case WatcherChangeTypes.Created:
                        GetDetailsAndAddPhotoAsync(filePath);
                        break;
                }
            });
        }

        private void ImagesDirectoryWatcher_Renamed([NotNull] object sender, [NotNull] RenamedEventArgs renamedEventArgs)
        {
            if (!IsImage(renamedEventArgs.FullPath))
                return;
            DispatcherHelper.CheckBeginInvokeOnUI(() => { RenamePhotoAsync(renamedEventArgs.OldFullPath, renamedEventArgs.FullPath); });
        }

        public void Dispose()
        {
            CancelCurrentTask();
            cancellationTokenSource.Dispose();
            imagesDirectoryWatcher.Dispose();
            imagesDirectoryWatcher.Created -= ImagesDirectoryWatcher_Changed;
            imagesDirectoryWatcher.Deleted -= ImagesDirectoryWatcher_Changed;
            imagesDirectoryWatcher.Renamed -= ImagesDirectoryWatcher_Renamed;
        }

        #endregion
    }
}