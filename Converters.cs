using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PhotoReviewer
{
    /// <summary>
    /// Converts an exposure time from a decimal (e.g. 0.0125) into a string (e.g. 1/80)
    /// </summary>
    public class ExposureTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                var exposure = Math.Round(1 / (double)value);
                return $"1/{exposure}";
            }
            catch (NullReferenceException)
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetTypes, object parameter, CultureInfo culture)
        {
            var exposure = ((string)value).Substring(2);
            return 1 / decimal.Parse(exposure);
        }
    }

    /// <summary>
    /// Converts a lens aperture from a decimal into a human-preferred string (e.g. 1.8 becomes F1.8)
    /// </summary>
    public class LensApertureConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null)
            {
                return $"F{value:##.0}";
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetTypes, object parameter, CultureInfo culture)
        {
            if (!string.IsNullOrEmpty((string)value))
            {
                return decimal.Parse(((string)value).Substring(1));
            }
            return null;
        }
    }

    /// <summary>
    /// Converts a focal length from a decimal into a human-preferred string (e.g. 85 becomes 85mm)
    /// </summary>
    public class FocalLengthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null ? $"{value}mm" : string.Empty;
        }

        public object ConvertBack(object value, Type targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Converts an x,y size pair into a string value (e.g. 1600x1200)
    /// </summary>
    public class PhotoSizeConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            return values[0] == null || values[1] == null || values[0] == DependencyProperty.UnsetValue ||
                   values[1] == DependencyProperty.UnsetValue
                ? string.Empty
                : $"{values[0]}x{values[1]}";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            if ((string)value == string.Empty)
                return new object[2];
            var sSize = ((string)value).Split('x');

            var size = new object[2];
            size[0] = uint.Parse(sSize[0]);
            size[1] = uint.Parse(sSize[1]);
            return size;
        }
    }
}