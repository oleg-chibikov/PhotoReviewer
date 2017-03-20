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
using Common.Logging;
using GalaSoft.MvvmLight.Messaging;
using JetBrains.Annotations;
using Microsoft.VisualBasic.FileIO;
using PhotoReviewer.DAL.Contracts;
using PhotoReviewer.DAL.Contracts.Model;
using PhotoReviewer.Resources;
using Scar.Common;
using Scar.Common.Drawing;
using Scar.Common.Drawing.Data;
using Scar.Common.IO;

//TODO: Transactions, UoW for repositories
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
        private readonly IPhotoInfoRepository<FavoritedPhoto> favoritedPhotoRepository;

        [NotNull]
        private readonly CollectionViewSource filteredViewSource;

        [NotNull]
        private readonly ILog logger;

        [NotNull]
        private readonly IPhotoInfoRepository<MarkedForDeletionPhoto> markedForDeletionPhotoRepository;

        [NotNull]
        private readonly IMessenger messenger;

        [NotNull]
        private readonly IMetadataExtractor metadataExtractor;

        [NotNull]
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        [NotNull]
        private Task currentTask = Task.CompletedTask;

        private bool showOnlyMarked;

        public PhotoCollection([NotNull] IComparer<string> comparer,
            [NotNull] IPhotoInfoRepository<MarkedForDeletionPhoto> markedForDeletionPhotoRepository,
            [NotNull] IPhotoInfoRepository<FavoritedPhoto> favoritedPhotoRepository,
            [NotNull] IMessenger messenger,
            [NotNull] ILog logger,
            [NotNull] IMetadataExtractor metadataExtractor)
        {
            if (comparer == null)
                throw new ArgumentNullException(nameof(comparer));
            if (markedForDeletionPhotoRepository == null)
                throw new ArgumentNullException(nameof(markedForDeletionPhotoRepository));
            if (favoritedPhotoRepository == null)
                throw new ArgumentNullException(nameof(favoritedPhotoRepository));
            if (messenger == null)
                throw new ArgumentNullException(nameof(messenger));
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            if (metadataExtractor == null)
                throw new ArgumentNullException(nameof(metadataExtractor));
            this.comparer = comparer;
            this.markedForDeletionPhotoRepository = markedForDeletionPhotoRepository;
            this.favoritedPhotoRepository = favoritedPhotoRepository;
            this.messenger = messenger;
            this.logger = logger;
            this.metadataExtractor = metadataExtractor;
            CollectionChanged += PhotoCollection_CollectionChanged;
            filteredViewSource = new CollectionViewSource { Source = this };
        }

        [NotNull]
        public ICollectionView FilteredView => filteredViewSource.View;

        public int FavoritedCount => this.Count(x => x.Favorited);

        public int MarkedForDeletionCount => this.Count(x => x.MarkedForDeletion);

        public bool ShowOnlyMarked
        {
            get { return showOnlyMarked; }
            set
            {
                showOnlyMarked = value;
                if (!value)
                    filteredViewSource.Filter -= OnFilteredViewSourceOnFilter;
                else
                    filteredViewSource.Filter += OnFilteredViewSourceOnFilter;
            }
        }

        public event EventHandler<ProgressEventArgs> Progress;
        public event EventHandler<PhotoDeletedEventArgs> PhotoDeleted;
        
        public void CancelCurrentTask()
        {
            cancellationTokenSource.Cancel();
        }

        [NotNull]
        public async Task SetPathAsync([NotNull] string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            logger.Info($"Changing path to '{path}'...");
            var token = RecreateCancellationToken();
            Clear();
            if (string.IsNullOrWhiteSpace(path))
                return;
            if (!Directory.Exists(path))
            {
                messenger.Send(string.Format(Errors.DirecoryDoesNotExist, path), MessengerTokens.UserWarningToken);
                return;
            }
            var context = SynchronizationContext.Current;
            currentTask = Task.Run(() =>
            {
                var files = GetFilesFromDirectory(path);
                var totalCount = files.Count();
                logger.Debug($"There are {totalCount} files in this directory");
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
                            var photo = new Photo(details.Path, details.Metadata, details.MarkedForDeletion, details.Favorited, this, token);
                            if (token.IsCancellationRequested)
                                break;
                            Add(photo);
                        }
                    }, null);
                    FavoritedChanged();
                    MarkedForDeletionChanged();
                    OnProgress(100 * i / blocksCount);
                    return true;
                });
                GC.Collect();
            }, token);
            await currentTask;
        }

        private CancellationToken RecreateCancellationToken()
        {
            CancelCurrentTask();
            cancellationTokenSource.Dispose();
            cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;
            return token;
        }

        public void FavoritedChanged()
        {
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(FavoritedCount)));
        }

        public void MarkedForDeletionChanged()
        {
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(MarkedForDeletionCount)));
        }

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
                markedForDeletionPhotoRepository.Save(paths);
                favoritedPhotoRepository.Delete(paths);
            }
            else
            {
                foreach (var photo in photos)
                    photo.MarkedForDeletion = false;
                markedForDeletionPhotoRepository.Delete(photos.Select(x => x.FilePath).ToArray());
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
                favoritedPhotoRepository.Save(paths);
                markedForDeletionPhotoRepository.Delete(paths);
            }
            else
            {
                foreach (var photo in photos)
                    photo.Favorited = false;
                favoritedPhotoRepository.Delete(photos.Select(x => x.FilePath).ToArray());
            }
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
            cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;
            var data = photos.Select(x => new { x.Name, Path = x.FilePath, x.Metadata.DateImageTaken }).ToArray();
            currentTask = Task.Run(() =>
            {
                var i = 0;
                var count = data.Length;
                foreach (var item in data)
                {
                    RenamePathToDate(item.Name, item.Path, item.DateImageTaken);
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
            cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;
            var paths = this.Where(x => x.MarkedForDeletion).Select(x => x.FilePath).ToArray();
            if (!paths.Any())
            {
                messenger.Send(Errors.NothingToDelete, MessengerTokens.UserWarningToken);
                return;
            }
            currentTask = Task.Run(() =>
            {
                var i = 0;
                var count = paths.Length;
                foreach (var path in paths)
                {
                    if (File.Exists(path))
                        FileSystem.DeleteFile(path,
                            UIOption.OnlyErrorDialogs,
                            RecycleOption.SendToRecycleBin);
                    OnPhotoDeleted(path);
                    OnProgress(100 * ++i / count);
                }
                favoritedPhotoRepository.Delete(paths);
                markedForDeletionPhotoRepository.Delete(paths);
            }, token);
            await currentTask;
        }

        [NotNull]
        public async Task MoveFavoritedAsync()
        {
            if (!currentTask.IsCompleted)
            {
                messenger.Send(Errors.TaskInProgress, MessengerTokens.UserWarningToken);
                return;
            }

            cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;
            var paths = this.Where(x => x.Favorited).Select(x => x.FilePath).ToArray();
            if (!paths.Any())
            {
                messenger.Send(Errors.NothingToMove, MessengerTokens.UserWarningToken);
                return;
            }
            currentTask = Task.Run(() =>
            {
                var dir = Path.GetDirectoryName(paths.First()) + "\\Favorite\\";
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                var i = 0;
                var count = paths.Length;
                foreach (var path in paths)
                {
                    if (!File.Exists(path))
                        continue;
                    var newName = dir + Path.GetFileName(path);
                    if (!File.Exists(newName))
                        File.Copy(path, newName);
                    OnProgress(100 * ++i / count);
                }
                Process.Start(dir);
            }, token);
            await currentTask;
        }

        private sealed class PhotoDetails
        {
            public PhotoDetails([NotNull] string path, [NotNull] ExifMetadata metadata, bool markedForDeletion, bool favorited)
            {
                if (path == null)
                    throw new ArgumentNullException(nameof(path));
                if (metadata == null)
                    throw new ArgumentNullException(nameof(metadata));
                Path = path;
                Metadata = metadata;
                MarkedForDeletion = markedForDeletion;
                Favorited = favorited;
            }

            [NotNull]
            public string Path { get; }

            [NotNull]
            public ExifMetadata Metadata { get; }

            public bool MarkedForDeletion { get; }
            public bool Favorited { get; }
        }

        #region WatcherHandlers

        //TODO: what to do if currently loading? Await current task, then do this one.

        public async void GetDetailsAndAddPhotoAsync([NotNull] string path)
        {
            await currentTask;
            var details = GetPhotoDetails(path);
            var photo = new Photo(details.Path, details.Metadata, details.MarkedForDeletion, details.Favorited, this, CancellationToken.None);
            InsertAtProperIndex(photo);
        }

        public async void DeletePhotoAsync([NotNull] string path)
        {
            await currentTask;
            favoritedPhotoRepository.Delete(path);
            markedForDeletionPhotoRepository.Delete(path);
            var photo = this.SingleOrDefault(x => x.FilePath == path);
            if (photo == null)
                return;
            Remove(photo);
            MarkedForDeletionChanged();
            FavoritedChanged();
            OnPhotoDeleted(path);
        }

        public async void RenamePhotoAsync([NotNull] string oldPath, [NotNull] string newPath)
        {
            await currentTask;
            favoritedPhotoRepository.Rename(oldPath, newPath);
            markedForDeletionPhotoRepository.Rename(oldPath, newPath);
            var photo = this.SingleOrDefault(x => x.FilePath == oldPath);
            if (photo == null)
                return;
            Remove(photo);
            photo.ChangePath(newPath);
            InsertAtProperIndex(photo);
        }

        #endregion

        #region Private

        private IOrderedEnumerable<string> GetFilesFromDirectory(string directory)
        {
            var files = DirectoryUtility.GetFiles(directory, Constants.FilterExtensions)
                .OrderBy(f => f, comparer);
            return files;
        }

        private void RenamePathToDate([NotNull] string name, [NotNull] string path, [CanBeNull] DateTime? dateImageTaken)
        {
            var oldPath = path;
            if (!File.Exists(path) || !dateImageTaken.HasValue)
                return;
            var newName = dateImageTaken.Value.ToString("yyyy-MM-dd HH-mm-ss");
            if (newName == name)
                return;
            var dir = Path.GetDirectoryName(path);
            var extension = Path.GetExtension(path);
            var newPath = DirectoryUtility.GetFreeFileName($"{dir}\\{newName}{extension}");
            if (!File.Exists(newPath))
                File.Move(oldPath, newPath);
            //FileSystemWatcher will do the rest
        }

        private void OnProgress(int percent)
        {
            Progress?.Invoke(this, new ProgressEventArgs(percent));
        }

        private void OnPhotoDeleted([NotNull] string path)
        {
            PhotoDeleted?.Invoke(this, new PhotoDeletedEventArgs(path));
        }

        private void PhotoCollection_CollectionChanged([NotNull] object sender, [NotNull] NotifyCollectionChangedEventArgs e)
        {
            foreach (var photo in this)
                photo.OnCollectionChanged();
        }

        private static void OnFilteredViewSourceOnFilter(object s, FilterEventArgs e)
        {
            var photo = e.Item as Photo;
            e.Accepted = photo != null && photo.IsValuableOrNearby;
        }

        private PhotoDetails GetPhotoDetails([NotNull] string path)
        {
            var metadata = metadataExtractor.Extract(path);
            var markedForDeletion = markedForDeletionPhotoRepository.Check(path);
            var favorited = favoritedPhotoRepository.Check(path);
            return new PhotoDetails(path, metadata, markedForDeletion, favorited);
        }

        private void InsertAtProperIndex([NotNull] Photo photo)
        {
            //http://stackoverflow.com/questions/748596/finding-best-position-for-element-in-list
            var name = photo.Name;
            var index = Array.BinarySearch(this.Select(x => x.Name).ToArray(), name, comparer);
            var insertIndex = ~index;
            Insert(insertIndex, photo);
        }

        public void Dispose()
        {
            CancelCurrentTask();
            currentTask.Dispose();
            cancellationTokenSource.Dispose();
        }

        #endregion
    }
}