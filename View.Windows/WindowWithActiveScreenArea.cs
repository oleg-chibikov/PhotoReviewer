using System.Windows.Interop;
using PhotoReviewer.Contracts.View;
using Scar.Common.RateLimiting;
using Scar.Common.WPF.Core;

namespace PhotoReviewer.View.Windows;

public class WindowWithActiveScreenArea : BaseWindow, IWindowWithActiveScreenArea
{
    readonly RateLimiter _rateLimiter;

    public WindowWithActiveScreenArea()
    {
        _rateLimiter = new RateLimiter(SynchronizationContext.Current);

        LocationChanged += LocationChangedAction;
        LocationChangedAction(this, EventArgs.Empty);
        return;

        async void LocationChangedAction(object? sender, EventArgs e)
        {
            await _rateLimiter.ThrottleAsync(TimeSpan.FromMilliseconds(300), window => ActiveScreenArea = Screen.FromHandle(new WindowInteropHelper(window).Handle).WorkingArea, this).ConfigureAwait(true);
        }
    }

    public Rectangle ActiveScreenArea { get; private set; }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _rateLimiter.Dispose();
        }
    }
}