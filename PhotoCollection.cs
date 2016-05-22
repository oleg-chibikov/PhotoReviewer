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
                    photo.MarkedForDeletion = true;
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
                    photo.Favorited = true;
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
                    AddNewPhoto(new Photo(path, metadata, this));
                }, null);
            });
        }

        public void RenamePhoto([NotNull] string oldPath, [NotNull] string newPath)
        {
            DbProvider.Rename(oldPath, newPath);
            var photo = this.SingleOrDefault(x => x.Path == oldPath);
            if (photo == null)
                return;
            Remove(photo);
            photo.ChangePath(newPath);
            AddNewPhoto(photo);
        }

        private void AddNewPhoto([NotNull] Photo photo)
        {
            //http://stackoverflow.com/questions/748596/finding-best-position-for-element-in-list
            var name = photo.Name;
            var index = Array.BinarySearch(this.Select(x => x.Name).ToArray(), name, Comparer);
            var insertIndex = ~index;
            Insert(insertIndex, photo);
        }
    }
}