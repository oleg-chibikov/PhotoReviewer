using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using Easy.MessageHub;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.FileIO;
using PhotoReviewer.Contracts.DAL;
using PhotoReviewer.Contracts.DAL.Data;
using PhotoReviewer.Core;
using PhotoReviewer.Resources;
using PropertyChanged;
using Scar.Common;
using Scar.Common.Async;
using Scar.Common.Events;
using Scar.Common.ImageProcessing.ExifExtraction;
using Scar.Common.IO;
using Scar.Common.Messages;
using Scar.Common.RateLimiting;

// TODO: BlockingCollection for photos commands
namespace PhotoReviewer.ViewModel
{
    /// <summary>This class represents a collection of photos in a directory.</summary>
    public sealed class PhotoCollection : ObservableCollection<Photo>, IDisposable
    {
        const int MaxBlockSize = 25;

        const string OperationPostfix = "_TO_BE_MODIFIED.jpg";

        readonly IDirectoryWatcher _directoryWatcher;

        readonly IExifTool _exifTool;

        readonly ICancellationTokenSourceProvider _loadPathCancellationTokenSourceProvider;

        readonly ILogger _logger;

        readonly IMessageHub _messenger;

        readonly ICancellationTokenSourceProvider _operationsCancellationTokenSourceProvider;

        readonly IPhotoUserInfoRepository _photoUserInfoRepository;

        readonly IRateLimiter _refreshViewRateLimiter;

        readonly Predicate<object> _showOnlyMarkedFilter = x => ((Photo)x).IsValuable;

        readonly Func<FileLocation, PhotoUserInfo, CancellationToken, PhotoCollection, Photo> _photoFactory;

        readonly SynchronizationContext _syncContext = SynchronizationContext.Current ?? throw new InvalidOperationException("SynchronizationContext.Current is null");

        public PhotoCollection(
            IComparer comparer,
            IMessageHub messenger,
            ILogger<PhotoCollection> logger,
            IPhotoUserInfoRepository photoUserInfoRepository,
            IExifTool exifTool,
            IDirectoryWatcher directoryWatcher,
            ICancellationTokenSourceProvider loadPathCancellationTokenSourceProvider,
            ICancellationTokenSourceProvider operationsCancellationTokenSourceProvider,
            IRateLimiter refreshViewRateLimiter,
            Func<FileLocation, PhotoUserInfo, CancellationToken, PhotoCollection, Photo> photoFactory)
        {
            _loadPathCancellationTokenSourceProvider = loadPathCancellationTokenSourceProvider ?? throw new ArgumentNullException(nameof(loadPathCancellationTokenSourceProvider));
            _operationsCancellationTokenSourceProvider =
                operationsCancellationTokenSourceProvider ?? throw new ArgumentNullException(nameof(operationsCancellationTokenSourceProvider));
            _refreshViewRateLimiter = refreshViewRateLimiter ?? throw new ArgumentNullException(nameof(refreshViewRateLimiter));
            _photoFactory = photoFactory ?? throw new ArgumentNullException(nameof(photoFactory));
            _directoryWatcher = directoryWatcher ?? throw new ArgumentNullException(nameof(directoryWatcher));
            _exifTool = exifTool ?? throw new ArgumentNullException(nameof(exifTool));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _photoUserInfoRepository = photoUserInfoRepository ?? throw new ArgumentNullException(nameof(photoUserInfoRepository));

            CollectionChanged += async (s, e) => await refreshViewRateLimiter.ThrottleAsync(TimeSpan.FromMilliseconds(300), Notify).ConfigureAwait(true);
            FilteredView = (ListCollectionView)CollectionViewSource.GetDefaultView(this);
            FilteredView.CustomSort = comparer ?? throw new ArgumentNullException(nameof(comparer));

            _directoryWatcher.FileAdded += DirectoryWatcher_FileAddedAsync;
            _directoryWatcher.FileDeleted += DirectoryWatcher_FileDeletedAsync;
            _directoryWatcher.FileRenamed += DirectoryWatcher_FileRenamedAsync;
            _exifTool.Progress += ExifTool_Progress;
            _exifTool.Error += ExifTool_Error;
        }

        public event EventHandler<EventArgs>? AllPhotosLoaded;

        public event EventHandler<EventArgs>? PhotoNotification;

        public event EventHandler<ProgressEventArgs>? Progress;

        [DoNotNotify]
        public ListCollectionView FilteredView { get; }

        public int FavoritedCount { get; set; }

        public int MarkedForDeletionCount { get; set; }

        public void Dispose()
        {
            _directoryWatcher.FileAdded -= DirectoryWatcher_FileAddedAsync;
            _directoryWatcher.FileDeleted -= DirectoryWatcher_FileDeletedAsync;
            _directoryWatcher.FileRenamed -= DirectoryWatcher_FileRenamedAsync;
            _exifTool.Progress -= ExifTool_Progress;
            _exifTool.Error -= ExifTool_Error;
            CancelCurrentTasks();
        }

        public async Task SetDirectoryPathAsync(string directoryPath)
        {
            if (directoryPath == null)
            {
                throw new ArgumentNullException(nameof(directoryPath));
            }

            _logger.LogInformation("Changing directory path to {DirectoryPath}...", directoryPath);

            _directoryWatcher.SetDirectoryPath(directoryPath);

            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                _messenger.Publish(Errors.SelectDirectory.ToWarning());
                return;
            }

            if (!Directory.Exists(directoryPath))
            {
                _messenger.Publish(string
                    .Format(CultureInfo.InvariantCulture, Errors.DirectoryDoesNotExist, directoryPath).ToWarning());
                return;
            }

            Clear();

            // check for all operations to be completed before changing the path, but don't wait for the current path load task
            if (!_operationsCancellationTokenSourceProvider.CheckCompleted())
            {
                return;
            }

            await StartLongOperationAsync(
                _loadPathCancellationTokenSourceProvider,
                LoadPhotos,
                true).ConfigureAwait(true); // new operation cancels previous one
            return;

            ////In order to display the Prev photos of Marked ones there is the need to refresh filter after all photos are loaded
            // if (_showOnlyMarked)
            //    FilteredView.Refresh();
            void LoadPhotos(CancellationToken cancellationToken)
            {
                var ultimatePhotoUserInfo = _photoUserInfoRepository.GetUltimateInfo(directoryPath);
                FavoritedCount = MarkedForDeletionCount = 0;
                var total = 0;
                Task.Run(
                    () =>
                    {
                        Directory.EnumerateFiles(directoryPath).ForEach((_) => total++);
                    },
                    cancellationToken);
                Directory.EnumerateFiles(directoryPath)
                    .Where(x => Constants.FileExtensions.Contains(Path.GetExtension(x)))
                    .Select(filePath => new FileLocation(filePath)).ForEachIndexed((fileLocation, index) =>
                    {
                        if (!ultimatePhotoUserInfo.TryGetValue(fileLocation, out var photoUserInfo))
                        {
                            photoUserInfo = new PhotoUserInfo(false, false);
                        }

                        var photo = _photoFactory(fileLocation, photoUserInfo, cancellationToken, this);
                        _syncContext.Send(
                            t =>
                            {
                                Add(photo);
                            }, null);

                        if (total != 0)
                        {
                            OnProgress(index + 1, total);
                        }
                    });
                AllPhotosLoaded?.Invoke(this, EventArgs.Empty);
            }
        }

        public async Task ShiftDateAsync(Photo[] photos, TimeSpan shiftBy, bool plus, bool renameToDate)
        {
            _ = photos ?? throw new ArgumentNullException(nameof(photos));

            if (CheckEmpty(photos))
            {
                return;
            }

            if (!_loadPathCancellationTokenSourceProvider.CheckCompleted())
            {
                return;
            }

            // TODO: don't process photos without metadata (warn user)
            await StartLongOperationAsync(
                    _operationsCancellationTokenSourceProvider,
                    async token =>
                    {
                        // TODO: Mark photos as failed/processed until new operation
                        var notificationSupresser = _directoryWatcher.SupressNotification();
                        InitializeDateShift(photos);
                        try
                        {
                            // TODO: only one or store them?
                            var exifToolPatterns = photos.Select(x => x.FileLocation)
                                .Select(x => x.Directory)
                                .Distinct()
                                .Select(x => $"{x.AddTrailingBackslash()}*{OperationPostfix}")
                                .ToArray();
                            await _exifTool.ShiftDateAsync(shiftBy, plus, exifToolPatterns, false, token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            _logger.LogWarning("Date shift is cancelled");
                        }
                        catch (InvalidOperationException ex)
                        {
                            _messenger.Publish(Errors.DateShiftFailed.ToWarning());
                            _logger.LogWarning(ex, "Date shift failed");
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
            {
                await RenameToDateAsync(photos).ConfigureAwait(false);
            }

            ResetTempPhotoMarkers(photos);
        }

        public async Task RenameToDateAsync(Photo[] photos)
        {
            if (CheckEmpty(photos))
            {
                return;
            }

            if (!_loadPathCancellationTokenSourceProvider.CheckCompleted())
            {
                return;
            }

            await StartLongOperationAsync(
                    _operationsCancellationTokenSourceProvider,
                    async cancellationToken =>
                    {
                        var totalCount = photos.Length;
                        _logger.LogTrace("There are {FileCount} files in this directory", totalCount);
                        using (_directoryWatcher.SupressNotification())
                        {
                            await photos.RunByBlocksAsync(
                                 MaxBlockSize,
                                 async (block, index, blocksCount) =>
                                 {
                                     if (cancellationToken.IsCancellationRequested)
                                     {
                                         return false;
                                     }

                                     _logger.LogTrace("Processing block {BlockIndex} ({BlockLength} files)...", index, block.Length);
                                     var tasks = block.Select(x => RenamePhotoToDateAsync(x, cancellationToken));
                                     await Task.WhenAll(tasks).ConfigureAwait(false);

                                     OnProgress(index + 1, blocksCount);

                                     return true;
                                 }).ConfigureAwait(true);

                            RefreshViewAsync();
                        }
                    },
                    false)
                .ConfigureAwait(false);
        }

        public async Task DeleteMarkedAsync()
        {
            if (CheckEmpty(this.Where(x => x.MarkedForDeletion)))
            {
                return;
            }

            if (!_loadPathCancellationTokenSourceProvider.CheckCompleted())
            {
                return;
            }

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

        public async Task CopyFavoritedAsync()
        {
            if (CheckEmpty(this.Where(x => x.Favorited)))
            {
                return;
            }

            if (!_loadPathCancellationTokenSourceProvider.CheckCompleted())
            {
                return;
            }

            await StartLongOperationAsync(
                    _operationsCancellationTokenSourceProvider,
                    token =>
                    {
                        var favoriteDirectories = this.Select(x => x.FileLocation.FavoriteDirectory).Distinct().ToArray();

                        // Create directories firstly
                        foreach (var favoriteDirectory in favoriteDirectories.Where(favoriteDirectory => !Directory.Exists(favoriteDirectory)))
                        {
                            Directory.CreateDirectory(favoriteDirectory);
                        }

                        var i = 0;
                        var count = Count;
                        Parallel.ForEach(
                            this,
                            photo =>
                            {
                                CopyFileIfFavorited(photo);
                                OnProgress(Interlocked.Increment(ref i), count);
                            });

                        // open not more than 3 directories
                        foreach (var favoriteDirectory in favoriteDirectories.Take(3))
                        {
                            Process.Start(favoriteDirectory);
                        }
                    },
                    false)
                .ConfigureAwait(false);
        }

        public void CancelCurrentTasks()
        {
            _loadPathCancellationTokenSourceProvider.Cancel();
            _operationsCancellationTokenSourceProvider.Cancel();
        }

        public void ChangeFilter(bool showOnlyMarked)
        {
            FilteredView.Filter = !showOnlyMarked ? null : _showOnlyMarkedFilter;
        }

        public void MarkForDeletion(Photo[] photos)
        {
            _ = photos ?? throw new ArgumentNullException(nameof(photos));

            if (CheckEmpty(photos))
            {
                return;
            }

            var notMarked = photos.Where(x => !x.MarkedForDeletion).ToArray();
            if (notMarked.Length > 0)
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
                {
                    photo.MarkedForDeletion = false;
                }

                _photoUserInfoRepository.UnMarkForDeletion(photos.Select(x => x.FileLocation).ToArray());
            }
        }

        public void Favorite(Photo[] photos)
        {
            _ = photos ?? throw new ArgumentNullException(nameof(photos));

            if (CheckEmpty(photos))
            {
                return;
            }

            var notFavorited = photos.Where(x => !x.Favorited).ToArray();
            if (notFavorited.Length > 0)
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
                {
                    photo.Favorited = false;
                }

                _photoUserInfoRepository.UnFavorite(photos.Select(x => x.FileLocation).ToArray());
            }
        }

        static void DeleteFileIfMarked(Photo photo)
        {
            if (!photo.MarkedForDeletion || !File.Exists(photo.FileLocation.ToString()))
            {
                return;
            }

            FileSystem.DeleteFile(photo.FileLocation.ToString(), UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
        }

        static void ShiftDateInMetadata(TimeSpan shiftBy, bool plus, Photo photo)
        {
            photo.Metadata.DateImageTaken =
                plus ? photo.Metadata.DateImageTaken + shiftBy : photo.Metadata.DateImageTaken - shiftBy;

            photo.ReloadMetadata();
        }

        static void ResetTempPhotoMarkers(Photo[] photos)
        {
            foreach (var photo in photos)
            {
                photo.LastOperationFinished = photo.LastOperationFailed = false;
            }
        }

        static string? GetNameFromDate(Photo photo)
        {
            return photo.Metadata.DateImageTaken?.ToString("yyyy-MM-dd HH-mm-ss", CultureInfo.InvariantCulture);
        }

        void Notify()
        {
            _logger.LogTrace("Notifying photos...");
            foreach (var photo in this)
            {
                photo.MarkAsNotSynced();
            }

            // only after marking as not synced
            PhotoNotification?.Invoke(this, EventArgs.Empty);
        }

        async void RefreshViewAsync()
        {
            await _refreshViewRateLimiter.ThrottleAsync(
                TimeSpan.FromSeconds(300),
                () =>
                {
                    _logger.LogTrace("Refreshing view...");
                    _syncContext.Send(t => FilteredView.Refresh(), null);

                    // TODO: Set selected after view refreshes
                    Notify();
                }).ConfigureAwait(true);
        }

        void ExifTool_Progress(object? sender, FilePathProgressEventArgs e)
        {
            OnProgress(e.Current, e.Total);

            var photo = GetPhoto(e.FilePath);
            if (photo != null)
            {
                photo.LastOperationFinished = true;
            }
        }

        void ExifTool_Error(object? sender, FilePathErrorEventArgs e)
        {
            var photo = GetPhoto(e.FilePath);
            if (photo != null)
            {
                photo.LastOperationFailed = true;
            }
        }

        async void DirectoryWatcher_FileAddedAsync(object? sender, EventArgs<string> e)
        {
            if (e == null)
            {
                throw new ArgumentNullException(nameof(e));
            }

            var fileLocation = new FileLocation(e.Parameter);

            await WaitAllTasksAsync().ConfigureAwait(true);

            var photoUserInfo = _photoUserInfoRepository.Check(fileLocation);
            var photo = _photoFactory(fileLocation, photoUserInfo, CancellationToken.None, this);
            _syncContext.Send(x => Add(photo), null);
            RefreshViewAsync();
            await photo.LoadAdditionalInfoAsync(CancellationToken.None).ConfigureAwait(true);
        }

        async void DirectoryWatcher_FileDeletedAsync(object? sender, EventArgs<string> e)
        {
            if (e == null)
            {
                throw new ArgumentNullException(nameof(e));
            }

            var fileLocation = new FileLocation(e.Parameter);

            await WaitAllTasksAsync().ConfigureAwait(true);

            _photoUserInfoRepository.Delete(fileLocation);
            var photo = this.SingleOrDefault(x => x.FileLocation == fileLocation);
            if (photo == null)
            {
                return;
            }

            _syncContext.Send(x => Remove(photo), null);
            if (photo.MarkedForDeletion)
            {
                MarkedForDeletionCount--;
            }

            if (photo.Favorited)
            {
                FavoritedCount--;
            }
        }

        async void DirectoryWatcher_FileRenamedAsync(object? sender, EventArgs<FileRenamedArgs> e)
        {
            if (e == null)
            {
                throw new ArgumentNullException(nameof(e));
            }

            var oldFileLocation = new FileLocation(e.Parameter.OldPath);
            var newfileLocation = new FileLocation(e.Parameter.NewPath);

            await WaitAllTasksAsync().ConfigureAwait(true);

            var photo = this.SingleOrDefault(x => x.FileLocation == oldFileLocation);
            if (photo == null)
            {
                return;
            }

            _photoUserInfoRepository.Rename(photo.FileLocation, newfileLocation);
            _syncContext.Send(x => photo.FileLocation = newfileLocation, null);
            RefreshViewAsync();
        }

        async Task WaitAllTasksAsync()
        {
            await _loadPathCancellationTokenSourceProvider.CurrentTask.ConfigureAwait(false);
            await _operationsCancellationTokenSourceProvider.CurrentTask.ConfigureAwait(false);
        }

        async Task StartLongOperationAsync(ICancellationTokenSourceProvider provider, Action<CancellationToken> action, bool cancelCurrent)
        {
            await provider.StartNewTaskAsync(
                    token =>
                    {
                        OnProgress(0, 100);
                        action(token);
                        if (!token.IsCancellationRequested)
                        {
                            OnProgress(100, 100);
                        }
                    },
                    cancelCurrent)
                .ConfigureAwait(false);
        }

        async Task StartLongOperationAsync(ICancellationTokenSourceProvider provider, Func<CancellationToken, Task> func, bool cancelCurrent)
        {
            await provider.ExecuteOperationAsync(
                    async token =>
                    {
                        OnProgress(0, 100);
                        await func(token).ConfigureAwait(false);
                        if (!token.IsCancellationRequested)
                        {
                            OnProgress(100, 100);
                        }
                    },
                    cancelCurrent)
                .ConfigureAwait(false);
        }

        async Task RenamePhotoToDateAsync(Photo photo, CancellationToken cancellationToken)
        {
            if (!File.Exists(photo.FileLocation.ToString()))
            {
                return;
            }

            // As Metadata is needed to get the date of capture, there is is essential to wait until it's loaded before renaming
            await photo.LoadAdditionalInfoAsync(cancellationToken).ConfigureAwait(true);

            var newName = GetNameFromDate(photo);
            if (newName == null)
            {
                _logger.LogWarning("Cannot get new name for {Photo}", photo);
                return;
            }

            if (newName == photo.Name)
            {
                return;
            }

            var newFilePath = Path.Combine(photo.FileLocation.Directory, $"{newName}{photo.FileLocation.Extension}");
            RenameFile(photo, newFilePath);
        }

        void CopyFileIfFavorited(Photo photo)
        {
            if (!photo.Favorited || !File.Exists(photo.FileLocation.ToString()))
            {
                return;
            }

            var favoritedFilePath = photo.FileLocation.FavoriteFilePath;
            if (!File.Exists(favoritedFilePath))
            {
                File.Copy(photo.FileLocation.ToString(), favoritedFilePath);
            }
            else
            {
                _logger.LogWarning("Favorited file {FilePath} already exists, skipped copying", favoritedFilePath);
            }
        }

        bool CheckEmpty(IEnumerable<Photo> photos)
        {
            if (!photos.Any())
            {
                _messenger.Publish(Errors.NothingToProcess.ToWarning());
                return true;
            }

            return false;
        }

        void OnProgress(int current, int total)
        {
            Progress?.Invoke(this, new ProgressEventArgs(current, total));
        }

        void InitializeDateShift(Photo[] photos)
        {
            _logger.LogTrace("Initializing date shift...");
            foreach (var photo in photos)
            {
                AddTempPostfix(photo);
            }
        }

        void FinalizeDateShift(Photo[] photos, TimeSpan shiftBy, bool plus)
        {
            _logger.LogTrace("Finalizing date shift result...");
            foreach (var photo in photos)
            {
                if (!photo.LastOperationFailed)
                {
                    ShiftDateInMetadata(shiftBy, plus, photo);
                }

                RemoveTempPostfix(photo);
            }
        }

        void AddTempPostfix(Photo photo)
        {
            var newFilePath = photo.FileLocation + OperationPostfix;
            RenameFile(photo, newFilePath);
        }

        void RemoveTempPostfix(Photo photo)
        {
            if (!photo.FileLocation.ToString().EndsWith(OperationPostfix, StringComparison.InvariantCulture))
            {
                return;
            }

            var modifiedFilePath = photo.FileLocation.ToString();
            var originalFilePath = modifiedFilePath.Remove(modifiedFilePath.Length - OperationPostfix.Length);
            RenameFile(photo, originalFilePath);
        }

        void RenameFile(Photo photo, string newFilePath)
        {
            newFilePath = photo.FileLocation.ToString().RenameFile(newFilePath);
            var newfileLocation = new FileLocation(newFilePath);
            _photoUserInfoRepository.Rename(photo.FileLocation, newfileLocation);
            photo.FileLocation = newfileLocation;
        }

        Photo? GetPhoto(string filePath)
        {
            // TODO: Store dictionary for filepaths
            var photo = this.SingleOrDefault(x => x.FileLocation.ToString() == filePath);
            if (photo == null)
            {
                _logger.LogWarning("Photo not found in collection for filepath {FilePath}", filePath);
            }

            return photo;
        }
    }
}
