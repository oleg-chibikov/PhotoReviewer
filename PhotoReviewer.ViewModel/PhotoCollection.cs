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
using Common.Multithreading;
using GalaSoft.MvvmLight.Messaging;
using JetBrains.Annotations;
using Microsoft.VisualBasic.FileIO;
using PhotoReviewer.Contracts.DAL;
using PhotoReviewer.Core;
using PhotoReviewer.Resources;
using PropertyChanged;
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
        private const int MaxBlockSize = 25;

        private const string OperationPostfix = "_TO_BE_MODIFIED.jpg";

        [NotNull]
        private readonly IAppendable<Func<Task>> _additionalInfoLoaderQueue;

        [NotNull]
        private readonly IDirectoryWatcher _directoryWatcher;

        [NotNull]
        private readonly IExifTool _exifTool;

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
        private readonly IPhotoUserInfoRepository _photoUserInfoRepository;

        [NotNull]
        private readonly Action _refreshViewDebounced;

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
            [NotNull] IPhotoUserInfoRepository photoUserInfoRepository,
            [NotNull] IExifTool exifTool,
            [NotNull] ICancellationTokenSourceProvider cancellationTokenSourceProvider,
            [NotNull] IAppendable<Func<Task>> additionalInfoLoaderQueue,
            [NotNull] IDirectoryWatcher directoryWatcher)
        {
            _directoryWatcher = directoryWatcher ?? throw new ArgumentNullException(nameof(directoryWatcher));
            _additionalInfoLoaderQueue = additionalInfoLoaderQueue ?? throw new ArgumentNullException(nameof(additionalInfoLoaderQueue));
            _mainOperationsCancellationTokenSourceProvider = cancellationTokenSourceProvider ?? throw new ArgumentNullException(nameof(cancellationTokenSourceProvider));
            _exifTool = exifTool ?? throw new ArgumentNullException(nameof(exifTool));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _metadataExtractor = metadataExtractor ?? throw new ArgumentNullException(nameof(metadataExtractor));
            _lifetimeScope = lifetimeScope ?? throw new ArgumentNullException(nameof(lifetimeScope));
            _photoUserInfoRepository = photoUserInfoRepository ?? throw new ArgumentNullException(nameof(photoUserInfoRepository));
            CollectionChanged += PhotoCollection_CollectionChanged;
            FilteredView = (ListCollectionView) CollectionViewSource.GetDefaultView(this);
            FilteredView.CustomSort = comparer ?? throw new ArgumentNullException(nameof(comparer));

            _directoryWatcher.FileAdded += DirectoryWatcher_FileAdded;
            _directoryWatcher.FileDeleted += DirectoryWatcher_FileDeleted;
            _directoryWatcher.FileRenamed += DirectoryWatcher_FileRenamed;

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
        [DoNotNotify]
        public ListCollectionView FilteredView { get; }

        public int FavoritedCount { get; set; }

        public int MarkedForDeletionCount { get; set; }

        public void Dispose()
        {
            _directoryWatcher.FileAdded -= DirectoryWatcher_FileAdded;
            _directoryWatcher.FileDeleted -= DirectoryWatcher_FileDeleted;
            _directoryWatcher.FileRenamed -= DirectoryWatcher_FileRenamed;
            CancelCurrentTasks();
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

            _directoryWatcher.SetDirectoryPath(directoryPath);

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
                    _logger.Trace($"There are {totalCount} files in this directory");
                    if (files.Any())
                    {
                        files.RunByBlocks(
                            MaxBlockSize,
                            (block, index, blocksCount) =>
                            {
                                if (token.IsCancellationRequested)
                                    return false;
                                _logger.Trace($"Processing block {index} ({block.Length} files)...");
                                var photos = block.Select(filePath => _lifetimeScope.Resolve<Photo>(new TypedParameter(typeof(string), filePath), new TypedParameter(typeof(PhotoCollection), this))).ToArray();
                                _syncContext.Send(
                                    t =>
                                    {
                                        foreach (var photo in photos)
                                            Add(photo);
                                    },
                                    null);
                                EnqueueLoadAdditionalInfoTask(photos, index, token);
                                OnProgress(index + 1, blocksCount);
                                //Little delay to prevent freezing of UI thread
                                // ReSharper disable MethodSupportsCancellation - no cancellation is needed for this small delay because we need expensive try catch in that case
                                Task.Delay(10).Wait();
                                // ReSharper restore MethodSupportsCancellation
                                return true;
                            });
                        GC.Collect();
                    }
                },
                true);

            ////In order to display the Prev photos of Marked ones there is the need to refresh filter after all photos are loaded
            //if (_showOnlyMarked)
            //    FilteredView.Refresh();
        }

        public async Task ShiftDateAsync([NotNull] Photo[] photos, TimeSpan shiftBy, bool plus, bool renameToDate)
        {
            if (CheckEmpty(photos))
                return;

            //TODO: don't process photos without metadata (warn user)

            await StartLongOperationAsync(
                    async token =>
                    {
                        //TODO: Mark photos as failed/processed until new operation
                        var notificationSupresser = _directoryWatcher.SupressNotification();
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

                            await _exifTool.ShiftDateAsync(shiftBy, plus, directories, false, token).ConfigureAwait(false);

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
                            notificationSupresser.Dispose();
                        }
                    })
                .ConfigureAwait(false);
            if (renameToDate)
                await RenameToDateAsync(photos).ConfigureAwait(false);
            ResetTempPhotoMarkers();
        }

        [NotNull]
        public async Task RenameToDateAsync([NotNull] Photo[] photos)
        {
            if (CheckEmpty(photos))
                return;

            await StartLongOperationAsync(
                    token =>
                    {
                        var totalCount = photos.Length;
                        _logger.Trace($"There are {totalCount} files in this directory");
                        using (_directoryWatcher.SupressNotification())
                        {
                            photos.RunByBlocks(
                                MaxBlockSize,
                                (block, index, blocksCount) =>
                                {
                                    if (token.IsCancellationRequested)
                                        return false;

                                    _logger.Trace($"Processing block {index} ({block.Length} files)...");

                                    _syncContext.Send(
                                        t =>
                                        {
                                            foreach (var photo in block)
                                                RenamePhotoToDate(photo);
                                        },
                                        null);

                                    OnProgress(index + 1, blocksCount);

                                    return true;
                                });

                            _refreshViewDebounced();
                        }
                    })
                .ConfigureAwait(false);
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
                                DeleteFileIfMarked(photo);
                                OnProgress(Interlocked.Increment(ref i), count);
                            });
                    })
                .ConfigureAwait(false);
        }

        [NotNull]
        public async Task CopyFavoritedAsync()
        {
            if (CheckEmpty(this.Where(x => x.Favorited)))
                return;

            await StartLongOperationAsync(
                    token =>
                    {
                        var favoriteDirectories = this.Select(x => GetFavoriteDirectory(x.FilePath)).Distinct().ToArray();
                        //Create directories firstly
                        foreach (var favoriteDirectory in favoriteDirectories.Where(favoriteDirectory => !Directory.Exists(favoriteDirectory)))
                            Directory.CreateDirectory(favoriteDirectory);

                        var i = 0;
                        var count = Count;
                        Parallel.ForEach(
                            this,
                            photo =>
                            {
                                CopyFileIfFavorited(photo);
                                OnProgress(Interlocked.Increment(ref i), count);
                            });
                        //open not more than 3 directories
                        foreach (var favoriteDirectory in favoriteDirectories.Take(3))
                            Process.Start(favoriteDirectory);
                    })
                .ConfigureAwait(false);
        }

        #endregion

        #region Sync operations

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
                _photoUserInfoRepository.MarkForDeletion(paths);
            }
            else
            {
                foreach (var photo in photos)
                    photo.MarkedForDeletion = false;

                _photoUserInfoRepository.UnMarkForDeletion(photos.Select(x => x.FilePath).ToArray());
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
                _photoUserInfoRepository.Favorite(paths);
            }
            else
            {
                foreach (var photo in photos)
                    photo.Favorited = false;

                _photoUserInfoRepository.UnFavorite(photos.Select(x => x.FilePath).ToArray());
            }
        }

        #endregion

        #region WatcherHandlers

        private async void DirectoryWatcher_FileAdded(object sender, [NotNull] EventArgs<string> e)
        {
            if (e == null)
                throw new ArgumentNullException(nameof(e));
            var filePath = e.Parameter;
            await _mainOperationsCancellationTokenSourceProvider.CurrentTask.ConfigureAwait(false);
            var photo = _lifetimeScope.Resolve<Photo>(
                new TypedParameter(typeof(string), filePath),
                new TypedParameter(typeof(PhotoCollection), this),
                new TypedParameter(typeof(CancellationToken), CancellationToken.None));
            _syncContext.Send(x => Add(photo), null);
            _refreshViewDebounced();
            await LoadAdditionalInfoForPhotoAsync(photo, CancellationToken.None);
        }

        private async void DirectoryWatcher_FileDeleted(object sender, [NotNull] EventArgs<string> e)
        {
            if (e == null)
                throw new ArgumentNullException(nameof(e));
            var filePath = e.Parameter;
            await _mainOperationsCancellationTokenSourceProvider.CurrentTask.ConfigureAwait(false);
            _photoUserInfoRepository.Delete(filePath);
            var photo = this.SingleOrDefault(x => x.FilePath == filePath);
            if (photo == null)
                return;

            _syncContext.Send(x => Remove(photo), null);
            if (photo.MarkedForDeletion)
                MarkedForDeletionCount--;
            if (photo.Favorited)
                FavoritedCount--;
        }

        private async void DirectoryWatcher_FileRenamed(object sender, [NotNull] EventArgs<Tuple<string, string>> e)
        {
            if (e == null)
                throw new ArgumentNullException(nameof(e));
            var oldFilePath = e.Parameter.Item1;
            var newFilePath = e.Parameter.Item2;
            await _mainOperationsCancellationTokenSourceProvider.CurrentTask.ConfigureAwait(false);
            var photo = this.SingleOrDefault(x => x.FilePath == oldFilePath);
            if (photo == null)
                return;

            _photoUserInfoRepository.Rename(photo.FilePath, newFilePath);
            _syncContext.Send(x => photo.SetFilePathAndName(newFilePath), null);
            _refreshViewDebounced();
        }

        #endregion

        #region Private

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
                    cancelCurrent)
                .ConfigureAwait(false);
        }

        private async Task StartLongOperationAsync([NotNull] Func<CancellationToken, Task> func, bool cancelCurrent = false)
        {
            await _mainOperationsCancellationTokenSourceProvider.ExecuteAsyncOperation(
                    async token =>
                    {
                        OnProgress(0, 100);
                        await func(token).ConfigureAwait(false);
                        if (!token.IsCancellationRequested)
                            OnProgress(100, 100);
                    },
                    cancelCurrent)
                .ConfigureAwait(false);
        }

        private void ResetTempPhotoMarkers()
        {
            foreach (var photo in this)
                photo.LastOperationFinished = photo.LastOperationFailed = false;
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
        private static string GetFavoritedFilePath([NotNull] string filePath)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            return Path.Combine(GetFavoriteDirectory(filePath), Path.GetFileName(filePath));
        }

        private const string FavoriteDirectoryName = "Favorite";

        [NotNull]
        private static string GetFavoriteDirectory([NotNull] string filePath)
        {
            var originalDirectory = Path.GetDirectoryName(filePath);
            // ReSharper disable once AssignNullToNotNullAttribute
            var favoriteDirectory = Path.Combine(originalDirectory, FavoriteDirectoryName);
            return favoriteDirectory;
        }

        private void CopyFileIfFavorited([NotNull] Photo photo)
        {
            if (!photo.Favorited || !File.Exists(photo.FilePath))
                return;
            //TODO: Move method to collection?
            var favoritedFilePath = GetFavoritedFilePath(photo.FilePath);
            if (!File.Exists(favoritedFilePath))
                File.Copy(photo.FilePath, favoritedFilePath);
        }

        private void DeleteFileIfMarked([NotNull] Photo photo)
        {
            if (!photo.MarkedForDeletion || !File.Exists(photo.FilePath))
                return;

            FileSystem.DeleteFile(photo.FilePath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
        }

        private bool CheckEmpty([NotNull] IEnumerable<Photo> photos)
        {
            if (!photos.Any())
            {
                _messenger.Send(Errors.NothingToProcess, MessengerTokens.UserWarningToken);
                return true;
            }

            return false;
        }

        private void EnqueueLoadAdditionalInfoTask([NotNull] IEnumerable<Photo> photosBlock, int blockIndex, CancellationToken token)
        {
            _additionalInfoLoaderQueue.Append(
                async () =>
                {
                    _logger.Trace($"Loading additional info for block {blockIndex}...");
                    foreach (var photo in photosBlock)
                        await LoadAdditionalInfoForPhotoAsync(photo, token).ConfigureAwait(false);
                    _logger.Trace($"Additional info for block {blockIndex} is loaded");
                });
        }

        private void OnProgress(int current, int total)
        {
            Progress?.Invoke(this, new ProgressEventArgs(current, total));
        }

        private void PhotoCollection_CollectionChanged([NotNull] object sender, [NotNull] NotifyCollectionChangedEventArgs e)
        {
            _notifyOpenPhotosDebounced();
        }

        private void FinalizeDateShift([NotNull] Photo[] photos, TimeSpan shiftBy, bool plus)
        {
            _logger.Trace("Finalizing date shift result...");
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

        private async Task LoadAdditionalInfoForPhotoAsync([NotNull] Photo photo, CancellationToken token)
        {
            if (photo == null)
                throw new ArgumentNullException(nameof(photo));
            if (token.IsCancellationRequested)
                return;
            var filePath = photo.FilePath;
            var favoritedFileExists = File.Exists(GetFavoritedFilePath(filePath));
            if (!photo.Favorited && favoritedFileExists)
            {
                _photoUserInfoRepository.Favorite(filePath);
                photo.Favorited = true;
            }
            photo.Metadata = await _metadataExtractor.ExtractAsync(filePath).ConfigureAwait(false);
            await photo.LoadThumbnailAsync(token).ConfigureAwait(false);
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
            _logger.Trace($"Adding postfix {OperationPostfix} to files (({photos.Length}))...");
            foreach (var photo in photos)
            {
                var newFilePath = photo.FilePath + OperationPostfix;
                RenameFile(photo, newFilePath);
            }
        }

        private void RenameFile([NotNull] Photo photo, [NotNull] string newFilePath)
        {
            newFilePath = photo.FilePath.RenameFile(newFilePath);
            _photoUserInfoRepository.Rename(photo.FilePath, newFilePath);
            photo.SetFilePathAndName(newFilePath);
        }

        #endregion
    }
}