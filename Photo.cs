using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using ExifLib;
using JetBrains.Annotations;

namespace PhotoReviewer
{
    /// <summary>
    ///     This class describes a single photo - its location, the image and
    ///     the metadata extracted from the image.
    /// </summary>
    public class Photo : DependencyObject
    {
        private readonly PhotoCollection collection;

        [CanBeNull]
        public Photo Next
        {
            get
            {
                var index = Index;
                return index == collection.Count - 1 || index == -1
                    ? null
                    : collection[index + 1];
            }
        }

        [CanBeNull]
        public Photo Prev
        {
            get
            {
                var index = Index;
                return index == 0 || index == -1
                    ? null
                    : collection[index - 1];
            }
        }

        public int Index => collection.IndexOf(this);

        public Photo(string source, BitmapSource thumbnail, PhotoCollection collection)
        {
            this.collection = collection;
            Source = source;
            Name = Path.GetFileNameWithoutExtension(source);
            MarkedForDeletion = DbProvider.IsMarkedForDeletion(Source);
            Thumbnail = thumbnail;
        }

        public static readonly DependencyProperty MarkedForDeletionProperty = DependencyProperty<Photo>.Register(x => x.MarkedForDeletion);

        public bool MarkedForDeletion
        {
            get { return (bool)GetValue(MarkedForDeletionProperty); }
            set { SetValue(MarkedForDeletionProperty, value); }
        }

        public string Source { get; }
        public string Name { get; }

        public BitmapSource Thumbnail { get; }

        public BitmapSource Image
        {
            get
            {
                var bytes = File.ReadAllBytes(Source);
                var bitmap = LoadImage(bytes, (int)SystemParameters.FullPrimaryScreenWidth);
                GC.Collect();
                return bitmap;
            }
        }

        public static BitmapSource LoadImage(byte[] imageData, int sizeAnchor = 0)
        {
            if (imageData == null || imageData.Length == 0) return null;
            var image = new BitmapImage();
            using (var mem = new MemoryStream(imageData))
            {
                mem.Position = 0;
                image.BeginInit();
                image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                image.CacheOption = BitmapCacheOption.OnLoad;
                if (sizeAnchor > 0)
                    image.DecodePixelWidth = sizeAnchor;
                image.UriSource = null;
                image.StreamSource = mem;
                image.EndInit();
            }
            image.Freeze();
            return image;
        }

        private ExifMetadata metadata;

        public ExifMetadata Metadata
        {
            get
            {
                if (metadata != null)
                    return metadata;
                try
                {
                    using (var reader = new ExifReader(Source))
                    {
                        DateTime datePictureTaken;
                        reader.GetTagValue(ExifTags.DateTimeDigitized, out datePictureTaken);
                        object width;
                        reader.GetTagValue(ExifTags.PixelXDimension, out width);
                        object height;
                        reader.GetTagValue(ExifTags.PixelYDimension, out height);
                        string cameraModel;
                        reader.GetTagValue(ExifTags.Model, out cameraModel);
                        object lensAperture;
                        reader.GetTagValue(ExifTags.MaxApertureValue, out lensAperture);
                        object focalLength;
                        reader.GetTagValue(ExifTags.FocalLength, out focalLength);
                        object isoSpeed;
                        reader.GetTagValue(ExifTags.PhotographicSensitivity, out isoSpeed);
                        object exposureTime;
                        reader.GetTagValue(ExifTags.ExposureTime, out exposureTime);
                        metadata = new ExifMetadata(width, height, datePictureTaken, cameraModel, lensAperture, focalLength, isoSpeed, exposureTime);
                    }
                }
                catch (Exception)
                {
                    // ignored
                }
                return metadata;
            }
        }

        public override string ToString()
        {
            return Source;
        }
    }
}