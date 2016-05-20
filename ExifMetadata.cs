using System;
using System.Windows.Media;
using ExifLib;

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
        private object width;
        private object height;
        private string cameraModel;
        private object lensAperture;
        private object focalLength;
        private object isoSpeed;
        private object exposureTime;

        public ExifMetadata(string source)
        {
            try
            {
                using (var reader = new ExifReader(source))
                {
                    DateTime dateImageTaken;
                    reader.GetTagValue(ExifTags.DateTimeDigitized, out dateImageTaken);
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
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        public object Width
        {
            get { return width; }
            set { width = value; }
        }

        public object Height
        {
            get { return height; }
            set { height = value; }
        }

        public string CameraModel
        {
            get { return cameraModel; }
            set { cameraModel = value; }
        }

        public object LensAperture
        {
            get { return lensAperture; }
            set { lensAperture = value; }
        }

        public object FocalLength
        {
            get { return focalLength; }
            set { focalLength = value; }
        }

        public object IsoSpeed
        {
            get { return isoSpeed; }
            set { isoSpeed = value; }
        }

        public object ExposureTime
        {
            get { return exposureTime; }
            set { exposureTime = value; }
        }

        public DateTime? DateImageTaken { get; set; }

        public Orientation? Orientation { get; set; }

        public ImageSource Thumbnail { get; set; }
    }
}