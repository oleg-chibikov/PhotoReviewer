using System.Windows;
using System.Windows.Data;
using PhotoReviewer.Contracts.View;
using PhotoReviewer.Contracts.ViewModel;
using PhotoReviewer.ViewModel;

namespace PhotoReviewer.View;

public sealed partial class PhotoWindow : IPhotoWindow
{
    readonly PhotoViewModel _photoViewModel;

    public PhotoWindow(IMainWindow mainWindow, PhotoViewModel photoViewModel)
    {
        Owner = mainWindow as Window ?? throw new ArgumentNullException(nameof(mainWindow));
        _photoViewModel = photoViewModel ?? throw new ArgumentNullException(nameof(photoViewModel));
        DataContext = photoViewModel;
        InitializeComponent();
        Show();
        Restore();
    }

    public IPhoto Photo => _photoViewModel.Photo;

    void Image_TargetUpdated(object? sender, DataTransferEventArgs e)
    {
        ZoomBorder.Reset();
    }
}