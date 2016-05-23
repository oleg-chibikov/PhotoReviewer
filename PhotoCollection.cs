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
    /// <summary>
    /// This class represents a collection of photos in a directory.
    /// </summary>
    public sealed class PhotoCollection : ObservableCollection<Photo>
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
        /// Sorts files as Windows does.
        /// </summary>
        [NotNull]
        private static readonly IComparer<string> Comparer = new WinComparer();

        public event EventHandler<ProgressEventArgs> Progress;

        [NotNull]
        public readonly DbProvider DbProvider = new DbProvider();

        [NotNull]
        public IList<PhotoView> PhotoViews { private get; set; }

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
                                context.Send(x => { Add(new Photo(f.FullName, metadata, this)); }, null);
                            }
                            FavoritedChanged();
                            MarkedForDeletionChanged();
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
                var paths = notMarked.Select(x => x.Path).ToArray();
                DbProvider.Save(paths, DbProvider.OperationType.MarkForDeletion);
                DbProvider.Delete(paths, DbProvider.OperationType.Favorite);
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
                DbProvider.Delete(photos.Select(x => x.Path).ToArray(), DbProvider.OperationType.MarkForDeletion);
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
                var paths = notFavorited.Select(x => x.Path).ToArray();
                DbProvider.Save(paths, DbProvider.OperationType.Favorite);
                DbProvider.Delete(paths, DbProvider.OperationType.MarkForDeletion);
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
                DbProvider.Delete(photos.Select(x => x.Path).ToArray(), DbProvider.OperationType.Favorite);
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
            var data = photos.Select(x => new { x.Name, x.Path, x.Metadata.DateImageTaken }).ToArray();
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

        public void DeleteMarked()
        {
            var paths = this.Where(x => x.MarkedForDeletion).Select(x => x.Path).ToArray();
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
                    context.Send(t => { CloseViews(path); }, null);
                    OnProgress(100 * ++i / count);
                }
                DbProvider.Delete(paths);
            });
        }

        public void MoveFavorited()
        {
            var paths = this.Where(x => x.Favorited).Select(x => x.Path).ToArray();
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

        public void AddPhoto([NotNull] string path)
        {
            var context = SynchronizationContext.Current;
            Task.Run(() =>
            {
                var metadata = new ExifMetadata(path);
                context.Send(x => { AddPhoto(new Photo(path, metadata, this)); }, null);
            });
        }

        public void DeletePhoto([NotNull] string path)
        {
            DbProvider.Delete(path);
            var photo = this.SingleOrDefault(x => x.Path == path);
            if (photo == null)
                return;
            Remove(photo);
            CloseViews(path);
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
            photo.OnPositionInCollectionChanged();
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

        private void OnProgress(int percent)
        {
            Progress?.Invoke(this, new ProgressEventArgs(percent));
        }

        private void CloseViews(string path)
        {
            for (var i = 0; i < PhotoViews.Count; i++)
            {
                var view = PhotoViews[i];
                if (view.SelectedPhoto.Path == path)
                {
                    view.Close();
                    PhotoViews.Remove(view);
                    i--;
                }
            }
        }
    }
}