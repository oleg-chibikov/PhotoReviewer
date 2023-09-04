using System.Drawing;
using Scar.Common.WPF.View.Contracts;

namespace PhotoReviewer.Contracts.View
{
    public interface IWindowWithActiveScreenArea : IWindow
    {
        Rectangle ActiveScreenArea { get; }
    }
}
