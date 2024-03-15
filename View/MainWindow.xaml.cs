using PhotoReviewer.Contracts.View;
using PhotoReviewer.ViewModel;

namespace PhotoReviewer.View;

public sealed partial class MainWindow : IMainWindow
{
    public MainWindow(MainViewModel viewModel)
    {
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        PhotosList.PhotosListBox.Focus();
    }
}