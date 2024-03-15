using System.Windows;
using System.Windows.Controls.Primitives;
using Scar.Common.WPF.Screen;

namespace PhotoReviewer.Memories.Controls;

public class DynamicUniformGrid : UniformGrid
{
    public DynamicUniformGrid()
    {
        Loaded += DynamicUniformGrid_Loaded;
    }

    protected override Size MeasureOverride(Size constraint)
    {
        // Calculate the number of columns based on available width
        var columns = CalculateColumns(constraint.Width);

        // Update the Columns property
        if (columns != Columns)
        {
            Columns = columns;
        }

        // Call the base MeasureOverride
        return base.MeasureOverride(constraint);
    }

    static int CalculateColumns(double availableWidth)
    {
        // Calculate the ideal column width (adjust as needed)
        const double idealColumnWidth = 400; // Adjust as needed

        // Calculate the number of columns based on available width
        var columns = (int)Math.Max(1, availableWidth / idealColumnWidth);

        return columns;
    }

    void DynamicUniformGrid_Loaded(object? sender, RoutedEventArgs e)
    {
        // Limit the size of the DynamicUniformGrid to the current screen
        LimitSizeToScreen();
    }

    void LimitSizeToScreen()
    {
        var window = Window.GetWindow(this);
        if (window != null)
        {
            var screen = WPFScreen.GetScreenFrom(window);
            var workingArea = screen.WorkingArea;

            // Calculate the maximum allowable width and height for the grid
            var maxWidth = workingArea.Width;
            var maxHeight = workingArea.Height;

            // Set the maximum allowable width and height for the grid
            MaxWidth = maxWidth - 30;
            MaxHeight = maxHeight;
        }
    }
}