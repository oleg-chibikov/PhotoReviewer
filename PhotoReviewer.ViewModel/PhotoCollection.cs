using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using Autofac;
using Common.Logging;
using Easy.MessageHub;
using JetBrains.Annotations;
using Microsoft.VisualBasic.FileIO;
using PhotoReviewer.Contracts.DAL;
using PhotoReviewer.Contracts.DAL.Data;
using PhotoReviewer.Core;
using PhotoReviewer.Resources;
using PropertyChanged;
using Scar.Common;
using Scar.Common.Async;
using Scar.Common.Drawing.ExifTool;
using Scar.Common.Events;
using Scar.Common.IO;
using Scar.Common.Messages;

//TODO: BlockingCollection for photos commands

namespace PhotoReviewer.ViewModel
{
    /// <summary>This class represents a collection of photos in a directory.</summary>
    [UsedImplicitly]
    public sealed class PhotoCollection : ObservableCollection<Photo>, IDisposable
    {
        private const int MaxBlockSize = 25;

        [NotNull]
        private const string OperationPostfix = "_TO_BE_MODIFIED.jpg";

        [NotNull]
        private readonly IDirectoryWatcher _directoryWatcher;

        [NotNull]
        private readonly IExifTool _exifTool;

        [NotNull]
        private readonly ILifetimeScope _lifetimeScope;

        [NotNull]
        private readonly ICancellationTokenSourceProvider _loadPathCancellationTokenSourceProvider;

        [NotNull]
        private readonly ILog _logger;

        [NotNull]
        private readonly IMessageHub _messenger;

        [NotNull]
        private readonly ICancellationTokenSourceProvider _operationsCancellationTokenSourceProvider;

        [NotNull]
        private readonly IPhotoUserInfoRepository _photoUserInfoRepository;

        [NotNull]
        private readonly Predicate<object> _showOnlyMarkedFilter = x => ((Photo) x).IsValuable;

        [NotNull]
        private readonly SynchronizationContext _syncContext = SynchronizationContext.Current;

        [NotNull]
        private readonly IRateLimiter _refreshViewRateLimiter;

        public PhotoCollection(
            [NotNull] IComparer comparer,
            [NotNull] IMessageHub messenger,
            [NotNull] ILog logger,
            [NotNull] ILifetimeScope lifetimeScope,
            [NotNull] IPhotoUserInfoRepository photoUserInfoRepository,
            [NotNull] IExifTool exifTool,
            [NotNull] IDirectoryWatcher directoryWatcher,
            [NotNull] ICancellationTokenSourceProvider loadPathCancellationTokenSourceProvider,
            [NotNull] ICancellationTokenSourceProvider operationsCancellationTokenSourceProvider,
            [NotNull] IRateLimiter refreshViewRateLimiter)
        {
            _loadPathCancellationTokenSourceProvider = loadPathCancellationTokenSourceProvider ?? throw new ArgumentNullException(nameof(loadPathCancellationTokenSourceProvider));
            _operationsCancellationTokenSourceProvider = operationsCancellationTokenSourceProvider ?? throw new ArgumentNullException(nameof(operationsCancellationTokenSourceProvider));
            _refreshViewRateLimiter = refreshViewRateLimiter ?? throw new ArgumentNullException(nameof(refreshViewRateLimiter));
            _directoryWatcher = directoryWatcher ?? throw new ArgumentNullException(nameof(directoryWatcher));
            _exifTool = exifTool ?? throw new ArgumentNullException(nameof(exifTool));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _lifetimeScope = lifetimeScope ?? throw new ArgumentNullException(nameof(lifetimeScope));
            _photoUserInfoRepository = photoUserInfoRepository ?? throw new ArgumentNullException(nameof(photoUserInfoRepository));


            CollectionChanged += (s, e) => refreshViewRateLimiter.Throttle(TimeSpan.FromMilliseconds(300), Notify);
            FilteredView = (ListCollectionView) CollectionViewSource.GetDefaultView(this);
            FilteredView.CustomSort = comparer ?? throw new ArgumentNullException(nameof(comparer));

            _directoryWatcher.FileAdded += DirectoryWatcher_FileAdded;
            _directoryWatcher.FileDeleted += DirectoryWatcher_FileDeleted;
            _directoryWatcher.FileRenamed += DirectoryWatcher_FileRenamed;
            _exifTool.Progress += ExifTool_Progress;
            _exifTool.Error += ExifTool_Error;
        }

        void Notify()
        {
            _logger.Trace("Notifying photos...");
            foreach (var photo in this)
                photo.MarkAsNotSynced();
            //only after marking as not synced
            PhotoNotification?.Invoke(this, new EventArgs());
        }

        void RefreshView()
        {
            _refreshViewRateLimiter.Throttle(
                TimeSpan.FromSeconds(300),
                () =>
                {
                    _logger.Trace("Refreshing view...");
                    _syncContext.Send(t => FilteredView.Refresh(), null);
                    //TODO: Set selected after view refreshes
                    Notify();
                });
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
            _exifTool.Progress -= ExifTool_Progress;
            _exifTool.Error -= ExifTool_Error;
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
                _messenger.Publish(Errors.SelectDirectory.ToWarning());
                return;
            }

            if (!Directory.Exists(directoryPath))
            {
                _messenger.Publish(string.Format(Errors.DirectoryDoesNotExist, directoryPath).ToWarning());
                return;
            }

            Clear();

            //check for all operations to be completed before changing the path, but don't wait for the current path load task
            if (!_operationsCancellationTokenSourceProvider.CheckCompleted())
                return;

            await StartLongOperationAsync(
                _loadPathCancellationTokenSourceProvider,
                token =>
                {
                    var ultimatePhotoUserInfo = _photoUserInfoRepository.GetUltimateInfo(directoryPath);
                    FavoritedCount = MarkedForDeletionCount = 0;
                    var fileLocations = directoryPath.GetFiles(Constants.FilterExtensions).Select(filePath => new FileLocation(filePath)).ToArray();
                    var totalCount = fileLocations.Length;
                    _logger.Trace($"There are {totalCount} files in this directory");
                    if (fileLocations.Any())
                    {
                        fileLocations.RunByBlocks(
                            MaxBlockSize,
                            (block, index, blocksCount) =>
                            {
                                if (token.IsCancellationRequested)
                                    return false;

                                _logger.Trace($"Processing block {index} ({block.Length} files)...");
                                var photos = block.Select(
                                        fileLocation =>
                                        {
                                            if (!ultimatePhotoUserInfo.TryGetValue(fileLocation, out var photoUserInfo))
                                                photoUserInfo = new PhotoUserInfo(false, false);
                                            return _lifetimeScope.Resolve<Photo>(
                                                new TypedParameter(typeof(FileLocation), fileLocation),
                                                new TypedParameter(typeof(PhotoUserInfo), photoUserInfo),
                                                new TypedParameter(typeof(CancellationToken), token),
                                                new TypedParameter(typeof(PhotoCollection), this));
                                        })
                                    .ToArray();
                                _syncContext.Send(
                                    t =>
                                    {
                                        foreach (var photo in photos)
                                            Add(photo);
                                    },
                                    null);
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
                true); //new operation cancels previous one

            ////In order to display the Prev photos of Marked ones there is the need to refresh filter after all photos are loaded
            //if (_showOnlyMarked)
            //    FilteredView.Refresh();
        }

        public async Task ShiftDateAsync([NotNull] Photo[] photos, TimeSpan shiftBy, bool plus, bool renameToDate)
        {
            if (CheckEmpty(photos))
                return;

            if (!_loadPathCancellationTokenSourceProvider.CheckCompleted())
                return;

            //TODO: don't process photos without metadata (warn user)

            await StartLongOperationAsync(
                    _operationsCancellationTokenSourceProvider,
                    async token =>
                    {
                        //TODO: Mark photos as failed/processed until new operation
                        var notificationSupresser = _directoryWatcher.SupressNotification();
                        InitializeDateShift(photos);
                        try
                        {
                            //TODO: only one or store them?
                            var exifToolPatterns = photos.Select(x => x.FileLocation).Select(x => x.Directory).Distinct().Select(x => $"{x.AddTrailingBackslash()}*{OperationPostfix}").ToArray();
                            await _exifTool.ShiftDateAsync(shiftBy, plus, exifToolPatterns, false, token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            _logger.Warn("Date shift is cancelled");
                        }
                        catch (InvalidOperationException ex)
                        {
                            _messenger.Publish(Errors.DateShiftFailed.ToWarning());
                            _logger.Warn("Date shift failed", ex);
                        }
                        finally
                        {
                            FinalizeDateShift(photos, shiftBy, plus);
                            notificationSupresser.Dispose();
                        }
                    },
                    false)
                .ConfigureAwait(false);
            if (renameToDate)
                await RenameToDateAsync(photos).ConfigureAwait(false);
            ResetTempPhotoMarkers(photos);
        }

        [NotNull]
        public async Task RenameToDateAsync([NotNull] Photo[] photos)
        {
            if (CheckEmpty(photos))
                return;

            if (!_loadPathCancellationTokenSourceProvider.CheckCompleted())
                return;

            await StartLongOperationAsync(
                    _operationsCancellationTokenSourceProvider,
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

                                    foreach (var photo in block)
                                        RenamePhotoToDateAsync(photo);

                                    OnProgress(index + 1, blocksCount);

                                    return true;
                                });

                            RefreshView();
                        }
                    },
                    false)
                .ConfigureAwait(false);
        }

        [NotNull]
        public async Task DeleteMarkedAsync()
        {
            if (CheckEmpty(this.Where(x => x.MarkedForDeletion)))
                return;

            if (!_loadPathCancellationTokenSourceProvider.CheckCompleted())
                return;

            await StartLongOperationAsync(
                    _operationsCancellationTokenSourceProvider,
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
                    },
                    false)
                .ConfigureAwait(false);
        }

        [NotNull]
        public async Task CopyFavoritedAsync()
        {
            if (CheckEmpty(this.Where(x => x.Favorited)))
                return;

            if (!_loadPathCancellationTokenSourceProvider.CheckCompleted())
                return;

            await StartLongOperationAsync(
                    _operationsCancellationTokenSourceProvider,
                    token =>
                    {
                        var favoriteDirectories = this.Select(x => x.FileLocation.FavoriteDirectory).Distinct().ToArray();
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
                    },
                    false)
                .ConfigureAwait(false);
        }

        #endregion

        #region Sync operations

        public void CancelCurrentTasks()
        {
            _loadPathCancellationTokenSourceProvider.Cancel();
            _operationsCancellationTokenSourceProvider.Cancel();
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

                var paths = notMarked.Select(x => x.FileLocation).ToArray();
                _photoUserInfoRepository.MarkForDeletion(paths);
            }
            else
            {
                foreach (var photo in photos)
                    photo.MarkedForDeletion = false;

                _photoUserInfoRepository.UnMarkForDeletion(photos.Select(x => x.FileLocation).ToArray());
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

                var paths = notFavorited.Select(x => x.FileLocation).ToArray();
                _photoUserInfoRepository.Favorite(paths);
            }
            else
            {
                foreach (var photo in photos)
                    photo.Favorited = false;

                _photoUserInfoRepository.UnFavorite(photos.Select(x => x.FileLocation).ToArray());
            }
        }

        #endregion

        #region ExifToolsHandlers

        private void ExifTool_Progress(object s, [NotNull] FilePathProgressEventArgs e)
        {
            OnProgress(e.Current, e.Total);

            var photo = GetPhoto(e.FilePath);
            if (photo != null)
                photo.LastOperationFinished = true;
        }

        private void ExifTool_Error(object s, [NotNull] FilePathErrorEventArgs e)
        {
            var photo = GetPhoto(e.FilePath);
            if (photo != null)
                photo.LastOperationFailed = true;
        }

        #endregion

        #region WatcherHandlers

        private async void DirectoryWatcher_FileAdded(object sender, [NotNull] EventArgs<string> e)
        {
            if (e == null)
                throw new ArgumentNullException(nameof(e));

            var fileLocation = new FileLocation(e.Parameter);

            await WaitAllTasks().ConfigureAwait(false);

            var photoUserInfo = _photoUserInfoRepository.Check(fileLocation);
            var photo = _lifetimeScope.Resolve<Photo>(
                new TypedParameter(typeof(FileLocation), fileLocation),
                new TypedParameter(typeof(PhotoUserInfo), photoUserInfo),
                new TypedParameter(typeof(PhotoCollection), this),
                new TypedParameter(typeof(CancellationToken), CancellationToken.None));
            _syncContext.Send(x => Add(photo), null);
            RefreshView();
            await photo.LoadAdditionalInfoTask.ConfigureAwait(false);
        }

        private async void DirectoryWatcher_FileDeleted(object sender, [NotNull] EventArgs<string> e)
        {
            if (e == null)
                throw new ArgumentNullException(nameof(e));

            var fileLocation = new FileLocation(e.Parameter);

            await WaitAllTasks().ConfigureAwait(false);

            _photoUserInfoRepository.Delete(fileLocation);
            var photo = this.SingleOrDefault(x => x.FileLocation == fileLocation);
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

            var oldFileLocation = new FileLocation(e.Parameter.Item1);
            var newfileLocation = new FileLocation(e.Parameter.Item2);

            await WaitAllTasks().ConfigureAwait(false);

            var photo = this.SingleOrDefault(x => x.FileLocation == oldFileLocation);
            if (photo == null)
                return;

            _photoUserInfoRepository.Rename(photo.FileLocation, newfileLocation);
            _syncContext.Send(x => photo.FileLocation = newfileLocation, null);
            RefreshView();
        }

        #endregion

        #region Private

        private async Task WaitAllTasks()
        {
            await _loadPathCancellationTokenSourceProvider.CurrentTask.ConfigureAwait(false);
            await _operationsCancellationTokenSourceProvider.CurrentTask.ConfigureAwait(false);
        }

        private async Task StartLongOperationAsync([NotNull] ICancellationTokenSourceProvider provider, [NotNull] Action<CancellationToken> action, bool cancelCurrent)
        {
            await provider.StartNewTask(
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

        private async Task StartLongOperationAsync([NotNull] ICancellationTokenSourceProvider provider, [NotNull] Func<CancellationToken, Task> func, bool cancelCurrent)
        {
            await provider.ExecuteAsyncOperation(
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

        private void ResetTempPhotoMarkers([NotNull] Photo[] photos)
        {
            foreach (var photo in photos)
                photo.LastOperationFinished = photo.LastOperationFailed = false;
        }

        private async Task RenamePhotoToDateAsync([NotNull] Photo photo)
        {
            if (!File.Exists(photo.FileLocation.ToString()))
                return;

            //As Metadata is needed to get the date of capture, there is is essential to wait until it's loaded before renaming
            await photo.LoadAdditionalInfoTask.ConfigureAwait(false);

            var newName = GetNameFromDate(photo);
            if (newName == null)
            {
                _logger.Warn($"Cannot get new name for {photo}");
                return;
            }
            if (newName == photo.Name)
                return;

            var newFilePath = $"{photo.FileLocation.Directory}\\{newName}{photo.FileLocation.Extension}";
            RenameFile(photo, newFilePath);
        }

        [CanBeNull]
        private static string GetNameFromDate([NotNull] Photo photo)
        {
            return photo.Metadata.DateImageTaken?.ToString("yyyy-MM-dd HH-mm-ss");
        }

        private void CopyFileIfFavorited([NotNull] Photo photo)
        {
            if (!photo.Favorited || !File.Exists(photo.FileLocation.ToString()))
                return;

            var favoritedFilePath = photo.FileLocation.FavoriteFilePath;
            if (!File.Exists(favoritedFilePath))
                File.Copy(photo.FileLocation.ToString(), favoritedFilePath);
            else
                _logger.Warn($"Favorited file {favoritedFilePath} already exists, skipped copying");
        }

        private void DeleteFileIfMarked([NotNull] Photo photo)
        {
            if (!photo.MarkedForDeletion || !File.Exists(photo.FileLocation.ToString()))
                return;

            FileSystem.DeleteFile(photo.FileLocation.ToString(), UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
        }

        private bool CheckEmpty([NotNull] IEnumerable<Photo> photos)
        {
            if (!photos.Any())
            {
                _messenger.Publish(Errors.NothingToProcess.ToWarning());
                return true;
            }

            return false;
        }

        private void OnProgress(int current, int total)
        {
            Progress?.Invoke(this, new ProgressEventArgs(current, total));
        }

        #region DateShift helpers

        private void InitializeDateShift([NotNull] Photo[] photos)
        {
            _logger.Trace("Initializing date shift...");
            foreach (var photo in photos)
                AddTempPostfix(photo);
        }

        private void FinalizeDateShift([NotNull] Photo[] photos, TimeSpan shiftBy, bool plus)
        {
            _logger.Trace("Finalizing date shift result...");
            foreach (var photo in photos)
            {
                if (!photo.LastOperationFailed)
                    ShiftDateInMetadata(shiftBy, plus, photo);

                RemoveTempPostfix(photo);
            }
        }

        private void AddTempPostfix([NotNull] Photo photo)
        {
            var newFilePath = photo.FileLocation + OperationPostfix;
            RenameFile(photo, newFilePath);
        }

        private void RemoveTempPostfix([NotNull] Photo photo)
        {
            if (!photo.FileLocation.ToString().EndsWith(OperationPostfix, StringComparison.InvariantCulture))
                return;

            var modifiedFilePath = photo.FileLocation.ToString();
            var originalFilePath = modifiedFilePath.Remove(modifiedFilePath.Length - OperationPostfix.Length);
            RenameFile(photo, originalFilePath);
        }

        #endregion

        private void RenameFile([NotNull] Photo photo, [NotNull] string newFilePath)
        {
            newFilePath = photo.FileLocation.ToString().RenameFile(newFilePath);
            var newfileLocation = new FileLocation(newFilePath);
            _photoUserInfoRepository.Rename(photo.FileLocation, newfileLocation);
            photo.FileLocation = newfileLocation;
        }

        private static void ShiftDateInMetadata(TimeSpan shiftBy, bool plus, [NotNull] Photo photo)
        {
            if (plus)
                photo.Metadata.DateImageTaken += shiftBy;
            else
                photo.Metadata.DateImageTaken -= shiftBy;
            photo.ReloadMetadata();
        }

        [CanBeNull]
        private Photo GetPhoto(string filePath)
        {
            //TODO: Store dictionary for filepaths
            var photo = this.SingleOrDefault(x => x.FileLocation.ToString() == filePath);
            if (photo == null)
                _logger.Warn($"Photo not found in collection for filepath {filePath}");
            return photo;
        }

        #endregion
    }
}