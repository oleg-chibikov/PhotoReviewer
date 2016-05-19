using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using ExifLib;
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
                            var files = directory.GetFiles("*.jpg").OrderBy(f => f.FullName, Comparer);
                            foreach (var f in files)
                            {
                                BitmapSource thumbnail = null;
                                try
                                {
                                    using (var reader = new ExifReader(f.FullName))
                                        thumbnail = Photo.LoadImage(reader.GetJpegThumbnailBytes());
                                }
                                catch (Exception)
                                {
                                    // ignored
                                }
                                context.Send(x =>
                                {
                                    Add(new Photo(f.FullName, thumbnail, this));
                                }, null);
                            }
                        });
                    }
                    catch (DirectoryNotFoundException)
                    {
                        MessageBox.Show("No Such Directory");
                    }
                    GC.Collect();
                }
                else
                    MessageBox.Show("No such directory");
            }
        }
    }
}