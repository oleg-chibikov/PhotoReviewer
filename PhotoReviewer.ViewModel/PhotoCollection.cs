using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using Autofac;
using Common.Logging;
using GalaSoft.MvvmLight.Messaging;
using JetBrains.Annotations;
using PhotoReviewer.Contracts.DAL;
using PhotoReviewer.Core;
using PhotoReviewer.Resources;
using Scar.Common;
using Scar.Common.Drawing.ExifTool;
using Scar.Common.Drawing.MetadataExtractor;
using Scar.Common.Events;
using Scar.Common.IO;

//TODO: BlockingCollection for photos commands

namespace PhotoReviewer.ViewModel
{
    /// <summary>This class represents a collection of photos in a directory.</summary>
    [UsedImplicitly]
    public sealed class PhotoCollection : ObservableCollection<Photo>, IDisposable
    {
        private const int MaxBlockSize = 10;

        [NotNull]
        private readonly IExifTool _exifTool;

        [NotNull]
        private readonly FileSystemWatcher _imagesDirectoryWatcher = new FileSystemWatcher
        {
            //TODO: polling every n seconds or use queue for handlers
            InternalBufferSize = 64 * 1024
        };

        [NotNull]
        private readonly ILifetimeScope _lifetimeScope;

        [NotNull]
        private readonly ILog _logger;

        [NotNull]
        private readonly ICancellationTokenSourceProvider _mainOperationsCancellationTokenSourceProvider;

        [NotNull]
        private readonly IMessenger _messenger;

        [NotNull]
        private readonly IMetadataExtractor _metadataExtractor;

        [NotNull]
        private readonly Action _notifyOpenPhotosDebounced;

        [NotNull]
        private readonly Action _refreshViewDebounced;

        [NotNull]
        private readonly IPhotoUserInfoRepository _repository;

        [NotNull]
        private readonly Predicate<object> _showOnlyMarkedFilter = x => ((Photo) x).IsValuable;

        [NotNull]
        private readonly SynchronizationContext _syncContext = SynchronizationContext.Current;

        public PhotoCollection(
            [NotNull] IComparer comparer,
            [NotNull] IMessenger messenger,
            [NotNull] ILog logger,
            [NotNull] IMetadataExtractor metadataExtractor,
            [NotNull] ILifetimeScope lifetimeScope,
            [NotNull] IPhotoUserInfoRepository repository,
            [NotNull] IExifTool exifTool,
            [NotNull] ICancellationTokenSourceProvider cancellationTokenSourceProvider)
        {
            _mainOperationsCancellationTokenSourceProvider = cancellationTokenSourceProvider ?? throw new ArgumentNullException(nameof(cancellationTokenSourceProvider));
            _exifTool = exifTool ?? throw new ArgumentNullException(nameof(exifTool));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _metadataExtractor = metadataExtractor ?? throw new ArgumentNullException(nameof(metadataExtractor));
            _lifetimeScope = lifetimeScope ?? throw new ArgumentNullException(nameof(lifetimeScope));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            CollectionChanged += PhotoCollection_CollectionChanged;
            FilteredView = (ListCollectionView) CollectionViewSource.GetDefaultView(this);
            FilteredView.CustomSort = comparer ?? throw new ArgumentNullException(nameof(comparer));
            _imagesDirectoryWatcher.Created += ImagesDirectoryWatcher_Changed;
            _imagesDirectoryWatcher.Deleted += ImagesDirectoryWatcher_Changed;
            _imagesDirectoryWatcher.Renamed += ImagesDirectoryWatcher_Renamed;

            Action notify = () =>
            {
                _logger.Trace("Notifying photos...");
                foreach (var photo in this)
                    photo.MarkAsNotSynced();
                //only after marking as not synced
                PhotoNotification?.Invoke(this, new EventArgs());
            };
            _notifyOpenPhotosDebounced = notify.Debounce();

            Action refresh = () =>
            {
                _logger.Trace("Refreshing view...");
                _syncContext.Send(t => FilteredView.Refresh(), null);
                //TODO: Set selected after view refreshes
                notify();
            };
            _refreshViewDebounced = refresh.Debounce();
        }

        [NotNull]
        public ListCollectionView FilteredView { get; }

        public int FavoritedCount { get; set; }

        public int MarkedForDeletionCount { get; set; }

        public void Dispose()
        {
            CancelCurrentTasks();
            _imagesDirectoryWatcher.Dispose();
            _imagesDirectoryWatcher.Created -= ImagesDirectoryWatcher_Changed;
            _imagesDirectoryWatcher.Deleted -= ImagesDirectoryWatcher_Changed;
            _imagesDirectoryWatcher.Renamed -= ImagesDirectoryWatcher_Renamed;
        }

        public void CancelCurrentTasks()
        {
            _mainOperationsCancellationTokenSourceProvider.Cancel();
        }

        public void ChangeFilter(bool showOnlyMarked)
        {
            FilteredView.Filter = !showOnlyMarked
                ? null
                : _showOnlyMarkedFilter;
        }

        public event EventHandler<EventArgs> PhotoNotification;

        public event EventHandler<ProgressEventArgs> Progress;

        #region Async operations

        [NotNull]
        public async Task SetDirectoryPathAsync([NotNull] string directoryPath)
        {
            if (directoryPath == null)
                throw new ArgumentNullException(nameof(directoryPath));

            _logger.Info($"Changing directory path to '{directoryPath}'...");
            SupressFileSystemEvents();
            _imagesDirectoryWatcher.Path = directoryPath;
            RestoreFileSystemEvents();

            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                _messenger.Send(Errors.SelectDirectory, MessengerTokens.UserWarningToken);
                return;
            }

            if (!Directory.Exists(directoryPath))
            {
                _messenger.Send(string.Format(Errors.DirectoryDoesNotExist, directoryPath), MessengerTokens.UserWarningToken);
                return;
            }

            Clear();

            await StartLongOperationAsync(
                token =>
                {
                    FavoritedCount = MarkedForDeletionCount = 0;
                    var files = directoryPath.GetFiles(Constants.FilterExtensions).ToArray();
                    var totalCount = files.Length;
                    _logger.Debug($"There are {totalCount} files in this directory");
                    if (files.Any())
                    {
                        var i = 0;
                        files.RunByBlocks(
                            MaxBlockSize,
                            (block, blocksCount) =>
                            {
                                if (token.IsCancellationRequested)
                                    return false;

                                _logger.Trace($"Processing block {i++} ({block.Length} files)...");
                                var detailsBlock = block.Select(GetPhotoDetails).ToArray();
                                _syncContext.Send(
                                    t =>
                                    {
                                        foreach (var photo in detailsBlock
                                            .Select(
                                                details => _lifetimeScope.Resolve<Photo>(
                                                    new TypedParameter(typeof(PhotoDetails), details),
                                                    new TypedParameter(typeof(PhotoCollection), this),
                                                    new TypedParameter(typeof(CancellationToken), token)))
                                            .TakeWhile(photo => !token.IsCancellationRequested))
                                            Add(photo);

                                        OnProgress(i, blocksCount);
                                    },
                                    null);
                                return true;
                            });
                    }
                    GC.Collect();
                },
                true);

            ////In order to display the Prev photos of Marked ones there is the need to refresh filter after all photos are loaded
            //if (_showOnlyMarked)
            //    FilteredView.Refresh();
        }

        private void SupressFileSystemEvents()
        {
            _logger.Debug("Supressing FS events...");
            _imagesDirectoryWatcher.EnableRaisingEvents = false;
        }

        private void RestoreFileSystemEvents()
        {
            _logger.Debug("Restoring FS events...");
            _imagesDirectoryWatcher.EnableRaisingEvents = true;
        }

        private const string OperationPostfix = "_TO_BE_MODIFIED.jpg";

        public async Task ShiftDateAsync([NotNull] Photo[] photos, TimeSpan shiftBy, bool plus, bool renameToDate)
        {
            if (CheckEmpty(photos))
                return;

            //TODO: don't process photos without metadata (warn user)

            await StartLongOperationAsync(
                async token =>
                {
                    //TODO: Mark photos as failed/processed until new operation
                    SupressFileSystemEvents();
                    AddTempPostfix(photos);
                    try
                    {
                        //TODO: only one or store them?
                        var directories = photos.Select(x => x.FilePath).Select(Path.GetDirectoryName).Distinct().Select(x => $"{x.AddTrailingBackslash()}*{OperationPostfix}").ToArray();

                        void Progress(object s, FilePathProgressEventArgs e)
                        {
                            OnProgress(e.Current, e.Total);

                            //TODO: Store dictionary for filepaths
                            var photo = this.SingleOrDefault(x => x.FilePath == e.FilePath);
                            if (photo == null)
                            {
                                _logger.Warn($"Photo not found in collection for filepath {e.FilePath}");
                                return;
                            }
                            photo.LastOperationFinished = true;
                        }

                        void Error(object s, FilePathErrorEventArgs e)
                        {
                            var photo = this.SingleOrDefault(x => x.FilePath == e.FilePath);
                            if (photo == null)
                            {
                                _logger.Warn($"Photo not found in collection for filepath {e.FilePath}");
                                return;
                            }
                            photo.LastOperationFailed = true;
                        }

                        _exifTool.Progress += Progress;
                        _exifTool.Error += Error;

                        await _exifTool.ShiftDateAsync(shiftBy, plus, directories, false, token);

                        _exifTool.Progress -= Progress;
                        _exifTool.Error -= Error;
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.Warn("Date shift is cancelled");
                    }
                    catch (InvalidOperationException ex)
                    {
                        _messenger.Send(Errors.DateShiftFailed, MessengerTokens.UserWarningToken);
                        _logger.Warn("Date shift failed", ex);
                    }
                    finally
                    {
                        FinalizeDateShift(photos, shiftBy, plus);
                        RestoreFileSystemEvents();
                    }
                });
            if (renameToDate)
                await RenameToDateAsync(photos);
            ResetTempPhotoMarkers();
        }

        private void FinalizeDateShift([NotNull] Photo[] photos, TimeSpan shiftBy, bool plus)
        {
            _logger.Debug("Finalizing date shift result...");
            foreach (var photo in photos)
            {
                if (!photo.LastOperationFailed)
                    ShiftDateInMetadata(shiftBy, plus, photo);

                if (photo.FilePath.EndsWith(OperationPostfix, StringComparison.InvariantCulture))
                {
                    var originalFilePath = photo.FilePath.Remove(photo.FilePath.Length - OperationPostfix.Length);
                    RenameFile(photo, originalFilePath);
                }
            }
        }

        private static void ShiftDateInMetadata(TimeSpan shiftBy, bool plus, [NotNull] Photo photo)
        {
            if (plus)
                photo.Metadata.DateImageTaken += shiftBy;
            else
                photo.Metadata.DateImageTaken -= shiftBy;
            photo.ReloadMetadata();
        }

        private void AddTempPostfix([NotNull] Photo[] photos)
        {
            _logger.Debug($"Adding postfix {OperationPostfix} to files (({photos.Length}))...");
            foreach (var photo in photos)
            {
                var newFilePath = photo.FilePath + OperationPostfix;
                RenameFile(photo, newFilePath);
            }
        }

        private void RenameFile([NotNull] Photo photo, [NotNull] string newFilePath)
        {
            newFilePath = photo.FilePath.RenameFile(newFilePath);
            _repository.Rename(photo.FilePath, newFilePath);
            photo.SetFilePathAndName(newFilePath);
        }

        [NotNull]
        public async Task RenameToDateAsync([NotNull] Photo[] photos)
        {
            if (CheckEmpty(photos))
                return;

            await StartLongOperationAsync(
                token =>
                {
                    var i = 0;
                    var totalCount = photos.Length;
                    _logger.Debug($"There are {totalCount} files in this directory");
                    SupressFileSystemEvents();
                    photos.RunByBlocks(
                        MaxBlockSize,
                        (block, blocksCount) =>
                        {
                            if (token.IsCancellationRequested)
                                return false;

                            _logger.Trace($"Processing block {i++} ({block.Length} files)...");

                            _syncContext.Send(
                                t =>
                                {
                                    foreach (var photo in block)
                                        RenamePhotoToDate(photo);
                                },
                                null);

                            OnProgress(i, blocksCount);

                            return true;
                        });

                    _refreshViewDebounced();
                    RestoreFileSystemEvents();
                });
        }

        private async Task StartLongOperationAsync([NotNull] Action<CancellationToken> action, bool cancelCurrent = false)
        {
            await _mainOperationsCancellationTokenSourceProvider.StartNewTask(
                token =>
                {
                    OnProgress(0, 100);
                    action(token);
                    if (!token.IsCancellationRequested)
                        OnProgress(100, 100);
                },
                cancelCurrent);
        }

        private void ResetTempPhotoMarkers()
        {
            foreach (var photo in this)
                photo.LastOperationFinished = photo.LastOperationFailed = false;
        }

        private async Task StartLongOperationAsync([NotNull] Func<CancellationToken, Task> func, bool cancelCurrent = false)
        {
            await _mainOperationsCancellationTokenSourceProvider.ExecuteAsyncOperation(
                async token =>
                {
                    OnProgress(0, 100);
                    await func(token);
                    if (!token.IsCancellationRequested)
                        OnProgress(100, 100);
                },
                cancelCurrent);
        }

        private void RenamePhotoToDate([NotNull] Photo photo)
        {
            if (!File.Exists(photo.FilePath))
                return;

            var newName = GetNameFromDate(photo);
            if (newName == null)
                return;
            if (newName == photo.Name)
                return;

            var directoryName = Path.GetDirectoryName(photo.FilePath);
            var extension = Path.GetExtension(photo.FilePath);
            var newFilePath = $"{directoryName}\\{newName}{extension}";
            RenameFile(photo, newFilePath);
        }

        [CanBeNull]
        private static string GetNameFromDate([NotNull] Photo photo)
        {
            return photo.Metadata.DateImageTaken?.ToString("yyyy-MM-dd HH-mm-ss");
        }

        [NotNull]
        public async Task DeleteMarkedAsync()
        {
            if (CheckEmpty(this.Where(x => x.MarkedForDeletion)))
                return;

            await StartLongOperationAsync(
                token =>
                {
                    var i = 0;
                    var count = Count;
                    Parallel.ForEach(
                        this,
                        photo =>
                        {
                            photo.DeleteFileIfMarked();
                            OnProgress(Interlocked.Increment(ref i), count);
                        });
                });
        }

        [NotNull]
        public async Task CopyFavoritedAsync()
        {
            if (CheckEmpty(this.Where(x => x.Favorited)))
                return;

            await StartLongOperationAsync(
                token =>
                {
                    var favoriteDirectories = this.Select(x => PhotoDetails.GetFavoriteDirectory(x.FilePath)).Distinct().ToArray();
                    //Create directories firstly
                    foreach (var favoriteDirectory in favoriteDirectories.Where(favoriteDirectory => !Directory.Exists(favoriteDirectory)))
                        Directory.CreateDirectory(favoriteDirectory);

                    var i = 0;
                    var count = Count;
                    Parallel.ForEach(
                        this,
                        photo =>
                        {
                            photo.CopyFileIfFavorited();
                            OnProgress(Interlocked.Increment(ref i), count);
                        });
                    //open not more than 3 directories
                    foreach (var favoriteDirectory in favoriteDirectories.Take(3))
                        Process.Start(favoriteDirectory);
                });
        }

        #endregion

        #region Sync operations

        public void MarkForDeletion([NotNull] Photo[] photos)
        {
            if (CheckEmpty(photos))
                return;

            var notMarked = photos.Where(x => !x.MarkedForDeletion).ToArray();
            if (notMarked.Any())
            {
                foreach (var photo in notMarked)
                {
                    photo.MarkedForDeletion = true;
                    photo.Favorited = false;
                }

                var paths = notMarked.Select(x => x.FilePath).ToArray();
                _repository.MarkForDeletion(paths);
            }
            else
            {
                foreach (var photo in photos)
                    photo.MarkedForDeletion = false;

                _repository.UnMarkForDeletion(photos.Select(x => x.FilePath).ToArray());
            }
        }

        public void Favorite([NotNull] Photo[] photos)
        {
            if (CheckEmpty(photos))
                return;

            var notFavorited = photos.Where(x => !x.Favorited).ToArray();
            if (notFavorited.Any())
            {
                foreach (var photo in notFavorited)
                {
                    photo.Favorited = true;
                    photo.MarkedForDeletion = false;
                }

                var paths = notFavorited.Select(x => x.FilePath).ToArray();
                _repository.Favorite(paths);
            }
            else
            {
                foreach (var photo in photos)
                    photo.Favorited = false;

                _repository.UnFavorite(photos.Select(x => x.FilePath).ToArray());
            }
        }

        #endregion

        #region WatcherHandlers

        private async void OnFileAddedAsync([NotNull] string filePath)
        {
            await _mainOperationsCancellationTokenSourceProvider.CurrentTask;
            var details = GetPhotoDetails(filePath);
            var photo = _lifetimeScope.Resolve<Photo>(
                new TypedParameter(typeof(PhotoDetails), details),
                new TypedParameter(typeof(PhotoCollection), this),
                new TypedParameter(typeof(CancellationToken), CancellationToken.None));
            _syncContext.Send(x => Add(photo), null);
            _refreshViewDebounced();
        }

        private async void OnFileDeletedAsync([NotNull] string filePath)
        {
            await _mainOperationsCancellationTokenSourceProvider.CurrentTask;
            _repository.Delete(filePath);
            var photo = this.SingleOrDefault(x => x.FilePath == filePath);
            if (photo == null)
                return;

            _syncContext.Send(x => Remove(photo), null);
            if (photo.MarkedForDeletion)
                MarkedForDeletionCount--;
            if (photo.Favorited)
                FavoritedCount--;
        }

        private async void OnFileRenamedAsync([NotNull] string oldFilePath, [NotNull] string newFilePath)
        {
            await _mainOperationsCancellationTokenSourceProvider.CurrentTask;
            var photo = this.SingleOrDefault(x => x.FilePath == oldFilePath);
            if (photo == null)
                return;

            _repository.Rename(photo.FilePath, newFilePath);
            _syncContext.Send(x => photo.SetFilePathAndName(newFilePath), null);
            _refreshViewDebounced();
        }

        #endregion

        #region Private

        private bool CheckEmpty([NotNull] IEnumerable<Photo> photos)
        {
            if (!photos.Any())
            {
                _messenger.Send(Errors.NothingToProcess, MessengerTokens.UserWarningToken);
                return true;
            }

            return false;
        }

        private void OnProgress(int current, int total)
        {
            Progress?.Invoke(this, new ProgressEventArgs(current, total));
        }

        private void PhotoCollection_CollectionChanged([NotNull] object sender, [NotNull] NotifyCollectionChangedEventArgs e)
        {
            _notifyOpenPhotosDebounced();
        }

        [NotNull]
        private PhotoDetails GetPhotoDetails([NotNull] string filePath)
        {
            var metadata = _metadataExtractor.Extract(filePath);
            var photoUserInfo = _repository.Check(filePath);
            var details = new PhotoDetails(filePath, metadata, photoUserInfo.MarkedForDeletion, photoUserInfo.Favorited);
            if (!photoUserInfo.Favorited && details.Favorited)
                _repository.Favorite(filePath);
            return details;
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

            switch (fileSystemEventArgs.ChangeType)
            {
                case WatcherChangeTypes.Deleted:
                    OnFileDeletedAsync(filePath);
                    break;
                case WatcherChangeTypes.Created:
                    OnFileAddedAsync(filePath);
                    break;
            }
        }

        private void ImagesDirectoryWatcher_Renamed([NotNull] object sender, [NotNull] RenamedEventArgs renamedEventArgs)
        {
            if (!IsImage(renamedEventArgs.FullPath))
                return;

            OnFileRenamedAsync(renamedEventArgs.OldFullPath, renamedEventArgs.FullPath);
        }

        #endregion
    }
}