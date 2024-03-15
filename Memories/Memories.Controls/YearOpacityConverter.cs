using System.Globalization;
using System.Windows.Data;

namespace PhotoReviewer.Memories.Controls;

public class YearOpacityConverter : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        _ = values ?? throw new ArgumentNullException(nameof(values));

        if (values.Length < 2 || values[0] is not int year || values[1] is not int currentYear)
        {
            return null;
        }

        // Calculate opacity based on the distance from the current year
        double opacity;
        if (year == currentYear)
        {
            // Selected year has full opacity
            opacity = 1.0;
        }
        else
        {
            // Calculate opacity based on the distance from the selected year
            double distance = Math.Abs(currentYear - year) + 3; // 3 is to move the opacity away from peak
            opacity = Math.Max(0.3, 1.0 - (distance * 0.1));
        }

        return opacity;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}