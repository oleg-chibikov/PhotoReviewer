using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using JetBrains.Annotations;
using Microsoft.VisualBasic.FileIO;

namespace PhotoReviewer
{
    public class ProgressEventArgs
    {
        public ProgressEventArgs(int percent)
        {
            Percent = percent;
        }

        public int Percent { get; private set; }
    }

    /// <summary>
    /// This class represents a collection of photos in a directory.
    /// </summary>
    public sealed class PhotoCollection : ObservableCollection<Photo>
    {
        private sealed class PhotoDetails
        {
            public string Path { get; }
            public ExifMetadata Metadata { get; }
            public bool MarkedForDeletion { get; }
            public bool Favorited { get; }

            public PhotoDetails(string path, ExifMetadata metadata, bool markedForDeletion, bool favorited)
            {
                Path = path;
                Metadata = metadata;
                MarkedForDeletion = markedForDeletion;
                Favorited = favorited;
            }
        }

        /// <summary>
        /// Sorts files as Windows does.
        /// </summary>
        [NotNull]
        private static readonly IComparer<string> Comparer = new WinComparer();

        [NotNull]
        private readonly DbProvider dbProvider = new DbProvider();

        public PhotoCollection()
        {
            CollectionChanged += PhotoCollection_CollectionChanged;
        }

        public int FavoritedCount => this.Count(x => x.Favorited);

        public int MarkedForDeletionCount => this.Count(x => x.MarkedForDeletion);

        public string Path
        {
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    return;
                var directory = new DirectoryInfo(value);
                if (directory.Exists)
                {
                    Clear();
                    var context = SynchronizationContext.Current;
                    Task.Run(() =>
                    {
                        var files = directory.GetFiles("*.jpg").OrderBy(f => f.Name, Comparer);
                        RunByBlocks(files, 50, block =>
                        {
                            var detailsBlock = block.Select(x => GetPhotoDetails(x.FullName)).ToArray();
                            context.Send(t =>
                            {
                                foreach (var details in detailsBlock)
                                    Add(new Photo(details.Path, details.Metadata, details.MarkedForDeletion, details.Favorited, this));
                            }, null);
                        });
                        FavoritedChanged();
                        MarkedForDeletionChanged();
                        GC.Collect();
                    });
                }
                else
                    MessageBox.Show("No such directory");
            }
        }

        private static void RunByBlocks<T>([NotNull] IEnumerable<T> items, int maxBlockSize, [NotNull] Action<T[]> action)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            var arr = items as T[] ?? items.ToArray();
            if (arr.Length == 0)
                return;

            if (maxBlockSize <= 0)
                maxBlockSize = 100;

            for (var i = 0; i <= arr.Length / maxBlockSize; i++)
            {
                var part = arr.Skip(i * maxBlockSize).Take(maxBlockSize).ToArray();
                if (part.Length > 0)
                    action.Invoke(part);
            }
        }

        public event EventHandler<ProgressEventArgs> Progress;

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
                MessageBox.Show("Nothing to mark");
                OnProgress(100);
                return;
            }
            var notMarked = photos.Where(x => !x.MarkedForDeletion).ToArray();
            if (notMarked.Any())
            {
                var i = 0;
                var count = notMarked.Length;
                foreach (var photo in notMarked)
                {
                    photo.MarkedForDeletion = true;
                    photo.Favorited = false;
                    OnProgress(100 * ++i / count);
                }
                var paths = notMarked.Select(x => x.FilePath).ToArray();
                dbProvider.Save(paths, DbProvider.OperationType.MarkForDeletion);
                dbProvider.Delete(paths, DbProvider.OperationType.Favorite);
            }
            else
            {
                var i = 0;
                var count = photos.Length;
                foreach (var photo in photos)
                {
                    photo.MarkedForDeletion = false;
                    OnProgress(100 * ++i / count);
                }
                dbProvider.Delete(photos.Select(x => x.FilePath).ToArray(), DbProvider.OperationType.MarkForDeletion);
            }
        }

        public void Favorite([NotNull] Photo[] photos)
        {
             if (!photos.Any())
            {
                MessageBox.Show("Nothing to favorite");
                OnProgress(100);
                return;
            }
            var notFavorited = photos.Where(x => !x.Favorited).ToArray();
            if (notFavorited.Any())
            {
                var i = 0;
                var count = notFavorited.Length;
                foreach (var photo in notFavorited)
                {
                    photo.Favorited = true;
                    photo.MarkedForDeletion = false;
                    OnProgress(100 * ++i / count);
                }
                var paths = notFavorited.Select(x => x.FilePath).ToArray();
                dbProvider.Save(paths, DbProvider.OperationType.Favorite);
                dbProvider.Delete(paths, DbProvider.OperationType.MarkForDeletion);
            }
            else
            {
                var i = 0;
                var count = photos.Length;
                foreach (var photo in photos)
                {
                    photo.Favorited = false;
                    OnProgress(100 * ++i / count);
                }
                dbProvider.Delete(photos.Select(x => x.FilePath).ToArray(), DbProvider.OperationType.Favorite);
            }
        }

        public void RenameToDate([NotNull] Photo[] photos, Action<string> callback)
        {
            if (!photos.Any())
            {
                MessageBox.Show("Nothing to rename");
                OnProgress(100);
                return;
            }
            string newPath = null;
            var data = photos.Select(x => new { x.Name, Path = x.FilePath, x.Metadata.DateImageTaken }).ToArray();
            var context = SynchronizationContext.Current;
            Task.Run(() =>
            {
                var i = 0;
                var count = data.Length;
                foreach (var item in data)
                {
                    newPath = RenameToDate(item.Name, item.Path, item.DateImageTaken);
                    OnProgress(100 * ++i / count);
                }
                if (newPath != null)
                    context.Send(t => { callback(newPath); }, null);
            });
        }

        public void DeleteMarked([NotNull] Action<string> onDelete)
        {
            var paths = this.Where(x => x.MarkedForDeletion).Select(x => x.FilePath).ToArray();
            if (!paths.Any())
            {
                MessageBox.Show("Nothing to delete");
                OnProgress(100);
                return;
            }
            var context = SynchronizationContext.Current;
            Task.Run(() =>
            {
                var i = 0;
                var count = paths.Length;
                foreach (var path in paths)
                {
                    if (File.Exists(path))
                    {
                        FileSystem.DeleteFile(path,
                            UIOption.OnlyErrorDialogs,
                            RecycleOption.SendToRecycleBin);
                    }
                    context.Send(t => { onDelete(path); }, null);
                    OnProgress(100 * ++i / count);
                }
                dbProvider.Delete(paths);
            });
        }

        public void MoveFavorited()
        {
            var paths = this.Where(x => x.Favorited).Select(x => x.FilePath).ToArray();
            if (!paths.Any())
            {
                MessageBox.Show("Nothing to move");
                OnProgress(100);
                return;
            }
            Task.Run(() =>
            {
                var dir = System.IO.Path.GetDirectoryName(paths.First()) + "\\Favorite\\";
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                var i = 0;
                var count = paths.Length;
                foreach (var path in paths)
                {
                    if (!File.Exists(path))
                        continue;
                    var newName = dir + System.IO.Path.GetFileName(path);
                    if (!File.Exists(newName))
                        File.Copy(path, newName);
                    OnProgress(100 * ++i / count);
                }
                Process.Start(dir);
            });
        }

        public void GetDetailsAndAddPhoto([NotNull] string path)
        {
            var context = SynchronizationContext.Current;
            var details = GetPhotoDetails(path);
            Task.Run(() => context.Send(x =>
            {
                var photo = new Photo(details.Path, details.Metadata, details.MarkedForDeletion, details.Favorited, this);
                InsertAtProperIndex(photo);
            }, null));
        }

        public void DeletePhoto([NotNull] string path)
        {
            dbProvider.Delete(path);
            var photo = this.SingleOrDefault(x => x.FilePath == path);
            if (photo == null)
                return;
            Remove(photo);
            MarkedForDeletionChanged();
            FavoritedChanged();
        }

        public void RenamePhoto([NotNull] string oldPath, [NotNull] string newPath)
        {
            dbProvider.Rename(oldPath, newPath);
            var photo = this.SingleOrDefault(x => x.FilePath == oldPath);
            if (photo == null)
                return;
            Remove(photo);
            photo.ChangePath(newPath);
            InsertAtProperIndex(photo);
        }

        [CanBeNull]
        private static string RenameToDate([NotNull] string name, [NotNull] string path, [CanBeNull] DateTime? dateImageTaken)
        {
            var oldPath = path;
            if (!File.Exists(path) || !dateImageTaken.HasValue)
                return null;
            var newName = dateImageTaken.Value.ToString("yyyy-MM-dd hh-mm-ss");
            if (newName == name)
                return null;
            var dir = System.IO.Path.GetDirectoryName(path);
            var newPath = GetFreeFileName($"{dir}\\{newName}.jpg");
            if (!File.Exists(newPath))
                File.Move(oldPath, newPath);
            //FileSystemWatcher will do the rest
            return newPath;
        }

        [NotNull]
        private static string GetFreeFileName([NotNull] string fullPath)
        {
            var count = 1;

            var fileNameOnly = System.IO.Path.GetFileNameWithoutExtension(fullPath);
            var extension = System.IO.Path.GetExtension(fullPath);
            var path = System.IO.Path.GetDirectoryName(fullPath);
            if (path == null)
                throw new ArgumentException(nameof(fullPath));
            var newFullPath = fullPath;

            while (File.Exists(newFullPath))
            {
                var tempFileName = $"{fileNameOnly} ({count++})";
                newFullPath = System.IO.Path.Combine(path, tempFileName + extension);
            }
            return newFullPath;
        }

        private void OnProgress(int percent)
        {
            Progress?.Invoke(this, new ProgressEventArgs(percent));
        }

        private void PhotoCollection_CollectionChanged([NotNull]object sender, [NotNull]System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            foreach (var photo in this)
                photo.OnPositionInCollectionChanged();
        }

        private PhotoDetails GetPhotoDetails([NotNull] string path)
        {
            var metadata = new ExifMetadata(path);
            var markedForDeletion = dbProvider.Check(path, DbProvider.OperationType.MarkForDeletion);
            var favorited = dbProvider.Check(path, DbProvider.OperationType.Favorite);
            return new PhotoDetails(path, metadata, markedForDeletion, favorited);
        }

        private void InsertAtProperIndex([NotNull] Photo photo)
        {
            //http://stackoverflow.com/questions/748596/finding-best-position-for-element-in-list
            var name = photo.Name;
            var index = Array.BinarySearch(this.Select(x => x.Name).ToArray(), name, Comparer);
            var insertIndex = ~index;
            Insert(insertIndex, photo);
        }
    }
}