using System;
using JetBrains.Annotations;
using PhotoReviewer.View.Contracts;
using PhotoReviewer.ViewModel;

namespace PhotoReviewer.View
{
    [UsedImplicitly]
    public sealed partial class MainWindow : IMainWindow
    {
        public MainWindow([NotNull] MainViewModel viewModel)
        {
            if (viewModel == null)
                throw new ArgumentNullException(nameof(viewModel));
            DataContext = viewModel;
            InitializeComponent();
        }
    }
}