using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.VisualBasic.FileIO;
using MessageBox = System.Windows.MessageBox;

namespace PhotoReviewer
{
    /// <summary>
    /// This class represents a collection of photos in a directory.
    /// </summary>
    public class PhotoCollection : ObservableCollection<Photo>
    {
        /// <summary>
        /// Sorts files as Windows does.
        /// </summary>
        [NotNull]
        private static readonly IComparer<string> Comparer = new WinComparer();

        [NotNull]
        public readonly DbProvider DbProvider = new DbProvider();

        public int FavoritedCount => this.Count(x => x.Favorited);

        public int MarkedForDeletionCount => this.Count(x => x.MarkedForDeletion);

        public string Path
        {
            set
            {
                var directory = new DirectoryInfo(value);
                if (directory.Exists)
                {
                    Clear();
                    try
                    {
                        var context = SynchronizationContext.Current;
                        Task.Run(() =>
                        {
                            var files = directory.GetFiles("*.jpg").OrderBy(f => f.Name, Comparer);
                            foreach (var f in files)
                            {
                                var metadata = new ExifMetadata(f.FullName);
                                context.Send(x =>
                                {
                                    Add(new Photo(f.FullName, metadata, this));
                                }, null);
                            }
                        });
                    }
                    catch (DirectoryNotFoundException)
                    {
                        MessageBox.Show("No such directory");
                    }
                    GC.Collect();
                }
                else
                    MessageBox.Show("No such directory");
            }
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
            var notMarked = photos.Where(x => !x.MarkedForDeletion).ToArray();
            if (notMarked.Any())
            {
                foreach (var photo in notMarked)
                {
                    photo.MarkedForDeletion = true;
                    photo.Favorited = false;
                }
                var paths = notMarked.Select(x => x.Path).ToArray();
                DbProvider.Save(paths, DbProvider.OperationType.MarkForDeletion);
                DbProvider.Delete(paths, DbProvider.OperationType.Favorite);
            }
            else
            {
                foreach (var photo in photos)
                    photo.MarkedForDeletion = false;
                DbProvider.Delete(photos.Select(x => x.Path).ToArray(), DbProvider.OperationType.MarkForDeletion);
            }
        }

        public void Favorite([NotNull] Photo[] photos)
        {
            var notFavorited = photos.Where(x => !x.Favorited).ToArray();
            if (notFavorited.Any())
            {
                foreach (var photo in notFavorited)
                {
                    photo.Favorited = true;
                    photo.MarkedForDeletion = false;
                }
                var paths = notFavorited.Select(x => x.Path).ToArray();
                DbProvider.Save(paths, DbProvider.OperationType.Favorite);
                DbProvider.Delete(paths, DbProvider.OperationType.MarkForDeletion);
            }
            else
            {
                foreach (var photo in photos)
                    photo.Favorited = false;
                DbProvider.Delete(photos.Select(x => x.Path).ToArray(), DbProvider.OperationType.Favorite);
            }
        }
        
        public void RenameToDate(Action<string> callback, [NotNull]params Photo[] photos)
        {
            if (!photos.Any())
            {
                MessageBox.Show("Nothing to rename");
                return;
            }
            string newPath = null;
            var data = photos.Select(x => new { x.Name, x.Path, x.Metadata.DateImageTaken }).ToArray();
            var context = SynchronizationContext.Current;
            Task.Run(() =>
            {
                foreach (var item in data)
                    newPath = RenameToDate(item.Name, item.Path, item.DateImageTaken);
                if (newPath != null)
                    context.Send(t => { callback(newPath); }, null);
            });
        }

        public void DeleteMarked()
        {
            var paths = this.Where(x => x.MarkedForDeletion).Select(x => x.Path).ToArray();
            if (!paths.Any())
            {
                MessageBox.Show("Nothing to delete");
                return;
            }
            Task.Run(() =>
            {
                foreach (var path in paths)
                {
                    if (File.Exists(path))
                    {
                        FileSystem.DeleteFile(path,
                            UIOption.OnlyErrorDialogs,
                            RecycleOption.SendToRecycleBin);
                    }
                }
                DbProvider.Delete(paths);
            });
        }

        public void AddPhoto([NotNull] string path)
        {
            var context = SynchronizationContext.Current;
            Task.Run(() =>
            {
                var metadata = new ExifMetadata(path);
                context.Send(x =>
                {
                    AddPhoto(new Photo(path, metadata, this));
                }, null);
            });
        }

        public void DeletePhoto([NotNull] string path)
        {
            DbProvider.Delete(path);
            var photo = this.SingleOrDefault(x => x.Path == path);
            if (photo == null)
                return;
            Remove(photo);
            MarkedForDeletionChanged();
            FavoritedChanged();
        }

        public void RenamePhoto([NotNull] string oldPath, [NotNull] string newPath)
        {
            DbProvider.Rename(oldPath, newPath);
            var photo = this.SingleOrDefault(x => x.Path == oldPath);
            if (photo == null)
                return;
            Remove(photo);
            photo.ChangePath(newPath);
            AddPhoto(photo);
        }

        private void AddPhoto([NotNull] Photo photo)
        {
            //http://stackoverflow.com/questions/748596/finding-best-position-for-element-in-list
            var name = photo.Name;
            var index = Array.BinarySearch(this.Select(x => x.Name).ToArray(), name, Comparer);
            var insertIndex = ~index;
            Insert(insertIndex, photo);
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
    }
}