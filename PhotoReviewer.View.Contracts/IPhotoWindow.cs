using Scar.Common.WPF;

namespace PhotoReviewer.View.Contracts
{
    public interface IPhotoWindow : IWindow
    {
        string PhotoPath { get; }
    }
}