using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
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

        public void DeletePhoto([NotNull] string path)
        {
            DbProvider.Delete(path);
            var photo = this.SingleOrDefault(x => x.Source == path);
            if (photo != null)
                Remove(photo);
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
            var photo = this.SingleOrDefault(x => x.Source == oldPath);
            if (photo == null)
                return;
            Remove(photo);
            photo.ChangeSource(newPath);
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