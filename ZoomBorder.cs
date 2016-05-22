using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PhotoReviewer
{
    public class ZoomBorder : Border
    {
        private const double MaxScale = 4;
        private const double DefaultScale = 1;
        private const double DefaultZoom = .4;
        //private readonly Duration zoomAnimationDuration = new Duration(TimeSpan.FromMilliseconds(50));

        private UIElement child;
        private Point originTopLeft;
        private Point originBottomRight;
        private Point start;
        private bool isReseted;
        private Func<Action, bool> zoomAction;

        private static TranslateTransform GetTranslateTransform(UIElement element)
        {
            return (TranslateTransform)((TransformGroup)element.RenderTransform)
                .Children.First(tr => tr is TranslateTransform);
        }

        private static ScaleTransform GetScaleTransform(UIElement element)
        {
            return (ScaleTransform)((TransformGroup)element.RenderTransform)
                .Children.First(tr => tr is ScaleTransform);
        }

        public override UIElement Child
        {
            get { return base.Child; }
            set
            {
                if (value != null && !Equals(value, Child))
                    Initialize(value);
                base.Child = value;
            }
        }

        private void Initialize(UIElement element)
        {
            child = element;
            if (child != null)
            {
                var group = new TransformGroup();
                var st = new ScaleTransform();
                group.Children.Add(st);
                var tt = new TranslateTransform();
                group.Children.Add(tt);
                child.RenderTransform = group;
                child.RenderTransformOrigin = new Point(0.0, 0.0);
                MouseWheel += child_MouseWheel;
                MouseLeftButtonDown += child_MouseLeftButtonDown;
                MouseLeftButtonUp += child_MouseLeftButtonUp;
                SizeChanged += ZoomBorder_SizeChanged;
                MouseMove += child_MouseMove;
                PreviewMouseDown += child_PreviewMouseButtonDown;
            }
        }

        public void Reset()
        {
            if (isReseted || child == null)
                return;
            // reset zoom
            var st = GetScaleTransform(child);
            st.ScaleX = 1.0;
            st.ScaleY = 1.0;

            // reset pan
            var tt = GetTranslateTransform(child);
            tt.X = 0.0;
            tt.Y = 0.0;
            isReseted = true;
        }

        public void SetAction(Func<Action, bool> action)
        {
            zoomAction = action;
        }

        #region Child Events

        private void child_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            ExecuteAndZoom(e.Delta > 0 ? DefaultZoom : -DefaultZoom, e);
        }

        private void child_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (child == null)
                return;
            if (e.ClickCount >= 2)
            {
                var st = GetScaleTransform(child);
                ExecuteAndZoom(st.ScaleX < MaxScale ? MaxScale - st.ScaleX : -st.ScaleX + DefaultScale, e);
            }
            else
            {
                var tt = GetTranslateTransform(child);
                var st = GetScaleTransform(child);
                start = e.GetPosition(this);
                originTopLeft = new Point(tt.X, tt.Y);
                originBottomRight = new Point(tt.X + child.RenderSize.Width * st.ScaleX, tt.Y + child.RenderSize.Height * st.ScaleY);
                Cursor = Cursors.Hand;
                child.CaptureMouse();
            }
        }

        private void child_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (child == null)
                return;
            child.ReleaseMouseCapture();
            Cursor = Cursors.Arrow;
        }

        private void child_PreviewMouseButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
                Reset();
        }

        private void child_MouseMove(object sender, MouseEventArgs e)
        {
            if (child == null)
                return;
            if (!child.IsMouseCaptured)
                return;
            var tt = GetTranslateTransform(child);
            var v = start - e.GetPosition(this);

            #region Check Bounds

            var newXLeft = originTopLeft.X - v.X;
            var newXRight = originBottomRight.X - v.X;
            var newYTop = originTopLeft.Y - v.Y;
            var newYBottom = originBottomRight.Y - v.Y;
            bool shiftX = true, shiftY = true;
            if (v.X < 0) //pull to the right
            {
                if (newXLeft > 0)
                    shiftX = false;
            }
            else //pull to the left
            {
                if (newXRight < child.RenderSize.Width)
                    shiftX = false;
            }
            if (v.Y < 0) //pull to the bottom
            {
                if (newYTop > 0)
                    shiftY = false;
            }
            else //pull to the top
            {
                if (newYBottom < child.RenderSize.Height)
                    shiftY = false;
            }

            #endregion

            if (shiftX)
                tt.X = newXLeft;
            if (shiftY)
                tt.Y = newYTop;
        }

        private void ZoomBorder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Reset();
            //var tt = GetTranslateTransform(child);
            //var st = GetScaleTransform(child);
            //var bottomRight = new Point(tt.X + child.RenderSize.Width * st.ScaleX, tt.Y + child.RenderSize.Height * st.ScaleY);
            //if (bottomRight.X < child.RenderSize.Width)
            //    tt.X += child.RenderSize.Width - bottomRight.X;

            //if (bottomRight.Y < child.RenderSize.Height)
            //    tt.Y += child.RenderSize.Height - bottomRight.Y;

            //tt.X = tt.X > 0 ? 0 : tt.X;
            //tt.Y = tt.Y > 0 ? 0 : tt.Y;
        }

        private void ExecuteAndZoom(double zoom, MouseEventArgs e)
        {
            Action z = () => Zoom(zoom, e);
            if (zoomAction != null)
            {
                if (!zoomAction(z))
                    z();
            }
            else
                z();
        }

        private void Zoom(double zoom, MouseEventArgs e)
        {
            if (child == null)
                return;
            var st = GetScaleTransform(child);
            var tt = GetTranslateTransform(child);

            var newScale = st.ScaleX + zoom;
            if (newScale < DefaultScale)
                zoom = DefaultScale - st.ScaleX;
            else if (newScale > MaxScale)
                zoom = st.ScaleX - MaxScale;

            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (zoom == 0)
                return;

            var relative = e.GetPosition(child);

            var abosoluteX = relative.X * st.ScaleX + tt.X;
            var abosoluteY = relative.Y * st.ScaleY + tt.Y;
            var newScaleX = st.ScaleX + zoom;
            var newScaleY = st.ScaleY + zoom;

            var diffX = relative.X * newScaleX;
            var diffY = relative.Y * newScaleY;

            var newX = abosoluteX - diffX;
            var newY = abosoluteY - diffY;

            #region Check Bounds For Zoom Out Only

            if (zoom <= 0)
            {
                var bottomRight = new Point(abosoluteX + child.RenderSize.Width * newScaleX, abosoluteY + child.RenderSize.Height * newScaleY);

                var newXRight = bottomRight.X - diffX;
                var newYBottom = bottomRight.Y - diffY;

                if (newXRight < child.RenderSize.Width)
                    newX += child.RenderSize.Width - newXRight;

                if (newYBottom < child.RenderSize.Height)
                    newY += child.RenderSize.Height - newYBottom;

                newX = newX > 0 ? 0 : newX;
                newY = newY > 0 ? 0 : newY;
            }

            #endregion

            #region Animate changes

            //var scaleXAnimation = new DoubleAnimation
            //{
            //    From = st.ScaleX,
            //    To = newScaleX,
            //    Duration = zoomAnimationDuration
            //};
            //var scaleYAnimation = new DoubleAnimation
            //{
            //    From = st.ScaleY,
            //    To = newScaleY,
            //    Duration = zoomAnimationDuration
            //};
            //st.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnimation);
            //st.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnimation);

            st.ScaleX += zoom;
            st.ScaleY += zoom;

            //var aX = new DoubleAnimation
            //{
            //    From = tt.X,
            //    To = newX,
            //    Duration = zoomAnimationDuration
            //};
            //var aY = new DoubleAnimation
            //{
            //    From = tt.Y,
            //    To = newY,
            //    Duration = zoomAnimationDuration
            //};

            //tt.BeginAnimation(TranslateTransform.XProperty, aX);
            //tt.BeginAnimation(TranslateTransform.YProperty, aY);

            tt.X = newX;
            tt.Y = newY;

            #endregion

            isReseted = false;
        }

        #endregion
    }
}