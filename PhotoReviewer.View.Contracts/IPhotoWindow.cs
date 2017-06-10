using Scar.Common.WPF.View.Contracts;

namespace PhotoReviewer.View.Contracts
{
    public interface IPhotoWindow : IWindow
    {
        string PhotoPath { get; }
    }
}