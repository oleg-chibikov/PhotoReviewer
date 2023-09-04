using System;
using System.Windows;
using PhotoReviewer.Contracts.View;

namespace PhotoReviewer.View.Windows
{
    public class ResizableWindow : WindowWithActiveScreenArea, IResizableWindow
    {
        public static readonly DependencyProperty IsFullHeightProperty = DependencyProperty.Register(
            nameof(IsFullHeight),
            typeof(bool),
            typeof(ResizableWindow),
            new PropertyMetadata(null));

        public ResizableWindow()
        {
            SizeChanged += ResizableWindow_SizeChanged;
        }

        public bool IsFullHeight
        {
            get => (bool)GetValue(IsFullHeightProperty);
            set => SetValue(IsFullHeightProperty, value);
        }

        void ResizableWindow_SizeChanged(object? sender, SizeChangedEventArgs e)
        {
            if (!e.HeightChanged)
            {
                return;
            }

            var fullHeight = ActiveScreenArea.Height;
            var newHeight = e.NewSize.Height;
            IsFullHeight = Math.Abs(newHeight - fullHeight) < 50;
        }
    }
}
