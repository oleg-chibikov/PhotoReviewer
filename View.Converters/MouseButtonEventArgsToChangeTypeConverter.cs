using System.Globalization;
using System.Windows.Data;
using System.Windows.Input;
using PhotoReviewer.ViewModel;

namespace PhotoReviewer.View.Converters;

public sealed class MouseButtonEventArgsToChangeTypeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not MouseButtonEventArgs args)
        {
            return ChangeType.None;
        }

        return args.ChangedButton switch
        {
            MouseButton.XButton1 => ChangeType.Next,
            MouseButton.XButton2 => ChangeType.Prev,
            _ => ChangeType.None
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}