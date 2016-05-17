using System;

namespace PhotoReviewer
{
    public class ExifMetadata
    {
        public ExifMetadata(object width, object height, DateTime? dateImageTaken, string cameraModel, object lensAperture, object focalLength, object isoSpeed, object exposureTime)
        {
            Width = width;
            Height = height;
            DateImageTaken = dateImageTaken;
            CameraModel = cameraModel;
            LensAperture = lensAperture;
            FocalLength = focalLength;
            IsoSpeed = isoSpeed;
            ExposureTime = exposureTime;
        }

        public object Width { get; set; }
        public object Height { get; set; }
        public DateTime? DateImageTaken { get; set; }
        public string CameraModel { get; set; }
        public object LensAperture { get; set; }
        public object FocalLength { get; set; }
        public object IsoSpeed { get; set; }
        public object ExposureTime { get; set; }
    }
}