using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
                if (directory.Exists)
                    Update();
                else
                    MessageBox.Show("No such directory");
            }
        }

        //private class AlphanumComparatorFast : IComparer<string>
        //{
        //    public int Compare(string x, string y)
        //    {
        //        var s1 = x;
        //        if (s1 == null)
        //            return 0;
        //        var s2 = y;
        //        if (s2 == null)
        //            return 0;

        //        var len1 = s1.Length;
        //        var len2 = s2.Length;
        //        var marker1 = 0;
        //        var marker2 = 0;

        //        // Walk through two the strings with two markers.
        //        while (marker1 < len1 && marker2 < len2)
        //        {
        //            var ch1 = s1[marker1];
        //            var ch2 = s2[marker2];

        //            // Some buffers we can build up characters in for each chunk.
        //            var space1 = new char[len1];
        //            var loc1 = 0;
        //            var space2 = new char[len2];
        //            var loc2 = 0;

        //            // Walk through all following characters that are digits or
        //            // characters in BOTH strings starting at the appropriate marker.
        //            // Collect char arrays.
        //            do
        //            {
        //                space1[loc1++] = ch1;
        //                marker1++;

        //                if (marker1 < len1)
        //                    ch1 = s1[marker1];
        //                else
        //                    break;
        //            } while (char.IsDigit(ch1) == char.IsDigit(space1[0]));

        //            do
        //            {
        //                space2[loc2++] = ch2;
        //                marker2++;

        //                if (marker2 < len2)
        //                    ch2 = s2[marker2];
        //                else
        //                    break;
        //            } while (char.IsDigit(ch2) == char.IsDigit(space2[0]));

        //            // If we have collected numbers, compare them numerically.
        //            // Otherwise, if we have strings, compare them alphabetically.
        //            var str1 = new string(space1);
        //            var str2 = new string(space2);

        //            int result;

        //            if (char.IsDigit(space1[0]) && char.IsDigit(space2[0]))
        //            {
        //                var thisNumericChunk = int.Parse(str1);
        //                var thatNumericChunk = int.Parse(str2);
        //                result = thisNumericChunk.CompareTo(thatNumericChunk);
        //            }
        //            else
        //                result = string.Compare(str1, str2, StringComparison.Ordinal);

        //            if (result != 0)
        //                return result;
        //        }
        //        return len1 - len2;
        //    }
        //}

        //private static readonly IComparer<string> Comparer = new AlphanumComparatorFast();

        private class WinComparer : IComparer<string>
        {
            [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
            private static extern int StrCmpLogicalW(string x, string y);

            public int Compare(string x, string y)
            {
                return StrCmpLogicalW(x, y);
            }
        }

        private static readonly IComparer<string> Comparer = new WinComparer();
        
        private void Update()
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
    }
}