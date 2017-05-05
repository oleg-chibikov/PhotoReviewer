using System;
using JetBrains.Annotations;
using PhotoReviewer.Contracts.View;
using PhotoReviewer.ViewModel;

namespace PhotoReviewer.View
{
    [UsedImplicitly]
    internal sealed partial class MainWindow : IMainWindow
    {
        public MainWindow([NotNull] MainViewModel viewModel)
        {
            DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            InitializeComponent();
            PhotosList.PhotosListBox.Focus();
        }
    }
}