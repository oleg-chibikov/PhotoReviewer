using Scar.Common.WPF.View.Contracts;

namespace PhotoReviewer.View.Contracts
{
    public interface IPhotoWindow : IWindow
    {
        //TODO: pass IPhoto instead of Photo
        object Photo { get; }
    }
}