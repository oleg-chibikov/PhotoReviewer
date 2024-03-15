using PhotoReviewer.Contracts.ViewModel;

namespace PhotoReviewer.Contracts.View;

public interface IPhotoWindow : IResizableWindow, IWindowWithActiveScreenArea
{
    IPhoto Photo { get; }
}