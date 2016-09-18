using System;
using System.IO;
using System.Threading;
using System.Windows.Media;
using ExifLib;
using JetBrains.Annotations;

namespace PhotoReviewer
{
    public enum Orientation
    {
        Straight = 1,
        Left = 6,
        Right = 8
    }

    public class ExifMetadata
    {
        private string cameraModel;
        private object exposureTime;
        private object focalLength;
        private object height;
        private object isoSpeed;
        private object lensAperture;
        private object width;

        public ExifMetadata([NotNull] string path)
        {
            const int attemptLimit = 3;
            var attempt = 0;
            while (attempt < attemptLimit)
                try
                {
                    using (var reader = new ExifReader(path))
                    {
                        DateTime dateImageTaken;
                        reader.GetTagValue(ExifTags.DateTimeOriginal, out dateImageTaken);
                        if (dateImageTaken != default(DateTime))
                            DateImageTaken = dateImageTaken;
                        reader.GetTagValue(ExifTags.PixelXDimension, out width);
                        reader.GetTagValue(ExifTags.PixelYDimension, out height);
                        reader.GetTagValue(ExifTags.Model, out cameraModel);
                        reader.GetTagValue(ExifTags.MaxApertureValue, out lensAperture);
                        reader.GetTagValue(ExifTags.FocalLength, out focalLength);
                        reader.GetTagValue(ExifTags.PhotographicSensitivity, out isoSpeed);
                        reader.GetTagValue(ExifTags.ExposureTime, out exposureTime);
                        ushort o;
                        reader.GetTagValue(ExifTags.Orientation, out o);
                        if (o != default(ushort))
                            Orientation = (Orientation)o;
                        Thumbnail = Photo.LoadImage(reader.GetJpegThumbnailBytes(), Orientation);
                        return; //break the cycle
                    }
                }
                catch (IOException)
                {
                    attempt++;
                    Thread.Sleep(100); //If image is new there should be a little time while it's created
                }
                catch (Exception)
                {
                    //Ignored
                }
        }

        [CanBeNull]
        public object Width
        {
            get { return width; }
            set { width = value; }
        }

        [CanBeNull]
        public object Height
        {
            get { return height; }
            set { height = value; }
        }

        [CanBeNull]
        public string CameraModel
        {
            get { return cameraModel; }
            set { cameraModel = value; }
        }

        [CanBeNull]
        public object LensAperture
        {
            get { return lensAperture; }
            set { lensAperture = value; }
        }

        [CanBeNull]
        public object FocalLength
        {
            get { return focalLength; }
            set { focalLength = value; }
        }

        [CanBeNull]
        public object IsoSpeed
        {
            get { return isoSpeed; }
            set { isoSpeed = value; }
        }

        [CanBeNull]
        public object ExposureTime
        {
            get { return exposureTime; }
            set { exposureTime = value; }
        }

        [CanBeNull]
        public DateTime? DateImageTaken { get; set; }

        [CanBeNull]
        public Orientation? Orientation { get; set; }

        [CanBeNull]
        public ImageSource Thumbnail { get; set; }
    }
}