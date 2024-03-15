using Scar.Common.WPF.Contracts;

namespace PhotoReviewer.Contracts.View;

public interface IResizableWindow : IWindow
{
    bool IsFullHeight { get; }
}