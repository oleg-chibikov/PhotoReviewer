using PhotoReviewer.Memories.ViewModel;

namespace PhotoReviewer.Memories.View;

public partial class GalleryWindow
{
    readonly GalleryViewModel _galleryViewModel;

    public GalleryWindow(GalleryViewModel galleryViewModel)
    {
        _galleryViewModel = galleryViewModel ?? throw new ArgumentNullException(nameof(galleryViewModel));
        DataContext = galleryViewModel;
        InitializeComponent();
        galleryViewModel.ScrollRequested += GalleryViewModel_ScrollRequested;
        Closed += GalleryWindow_Closed;
    }

    void GalleryWindow_Closed(object? sender, EventArgs e)
    {
        _galleryViewModel.ScrollRequested -= GalleryViewModel_ScrollRequested;
    }

    void GalleryViewModel_ScrollRequested(object? sender, EventArgs e)
    {
        ScrollViewer.ScrollToVerticalOffset(0);
    }
}
