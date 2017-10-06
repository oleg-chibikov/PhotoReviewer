using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using JetBrains.Annotations;
using PhotoReviewer.ViewModel;

namespace PhotoReviewer.View
{
    internal sealed class MouseButtonEventArgsToChangeTypeConverter : IValueConverter
    {
        [NotNull]
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var args = (MouseButtonEventArgs) value;
            if (args == null)
                return ChangeType.None;

            switch (args.ChangedButton)
            {
                case MouseButton.XButton1:
                    return ChangeType.Next;
                case MouseButton.XButton2:
                    return ChangeType.Prev;
                default:
                    return ChangeType.None;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>Converts an exposure time from a decimal (e.g. 0.0125) into a string (e.g. 1/80)</summary>
    internal sealed class ExposureTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null
                ? null
                : $"1/{Math.Round(1 / (double) value)}";
        }

        public object ConvertBack(object value, Type targetTypes, object parameter, CultureInfo culture)
        {
            return value == null
                ? (object) null
                : 1 / decimal.Parse(((string) value).Substring(2));
        }
    }

    /// <summary>Converts a lens aperture from a decimal into a human-preferred string (e.g. 1.8 becomes F1.8)</summary>
    internal sealed class LensApertureConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null
                ? $"F{value:##.0}"
                : null;
        }

        public object ConvertBack(object value, Type targetTypes, object parameter, CultureInfo culture)
        {
            return !string.IsNullOrEmpty((string) value)
                ? (object) decimal.Parse(((string) value).Substring(1))
                : null;
        }
    }

    /// <summary>Converts a focal length from a decimal into a human-preferred string (e.g. 85 becomes 85mm)</summary>
    internal sealed class FocalLengthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null
                ? $"{value}mm"
                : null;
        }

        public object ConvertBack(object value, Type targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>Converts an x,y size pair into a string value (e.g. 1600x1200)</summary>
    internal sealed class PhotoSizeConverter : IMultiValueConverter
    {
        [NotNull]
        public object Convert([NotNull] object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            return values[0] == null || values[1] == null || values[0] == DependencyProperty.UnsetValue || values[1] == DependencyProperty.UnsetValue
                ? string.Empty
                : $"{values[0]}x{values[1]}";
        }

        [NotNull]
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            if ((string) value == string.Empty)
                return new object[2];

            var sSize = ((string) value).Split('x');

            var size = new object[2];
            size[0] = uint.Parse(sSize[0]);
            size[1] = uint.Parse(sSize[1]);
            return size;
        }
    }
}