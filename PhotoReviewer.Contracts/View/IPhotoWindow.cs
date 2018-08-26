using PhotoReviewer.Contracts.ViewModel;
using Scar.Common.WPF.View.Contracts;

namespace PhotoReviewer.Contracts.View
{
    public interface IPhotoWindow : IWindow
    {
        IPhoto Photo { get; }
    }
}