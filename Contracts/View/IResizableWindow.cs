using Scar.Common.WPF.View.Contracts;

namespace PhotoReviewer.Contracts.View
{
    public interface IResizableWindow : IWindow
    {
        bool IsFullHeight { get; }
    }
}
