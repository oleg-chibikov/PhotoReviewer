using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using ExifLib;
using MessageBox = System.Windows.MessageBox;

namespace PhotoReviewer
{
    /// <summary>
    ///     This class represents a collection of photos in a directory.
    /// </summary>
    public class PhotoCollection : ObservableCollection<Photo>
    {
        private DirectoryInfo directory;

        public string Path
        {
            set
            {
                directory = new DirectoryInfo(value);
                Update();
            }
        }
        
        private void Update()
        {
            Clear();
            try
            {
                var context = SynchronizationContext.Current;
                Task.Run(() =>
                {
                    var files = directory.GetFiles("*.jpg");
                    foreach (var f in files)
                    {
                        BitmapSource thumbnail = null;
                        try
                        {
                            using (var reader = new ExifReader(f.FullName))
                                thumbnail = Photo.LoadImage(reader.GetJpegThumbnailBytes());
                        }
                        catch (Exception ex)
                        {
                            
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
    }
}