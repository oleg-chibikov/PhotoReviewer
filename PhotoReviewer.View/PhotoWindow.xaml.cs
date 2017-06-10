using System;
using System.Windows;
using System.Windows.Data;
using JetBrains.Annotations;
using PhotoReviewer.Contracts.View;
using PhotoReviewer.Contracts.ViewModel;
using PhotoReviewer.ViewModel;

namespace PhotoReviewer.View
{
    [UsedImplicitly]
    internal sealed partial class PhotoWindow : IPhotoWindow
    {
        [NotNull]
        private readonly PhotoViewModel _photoViewModel;

        public PhotoWindow([NotNull] Window mainWindow, [NotNull] PhotoViewModel photoViewModel)
        {
            Owner = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _photoViewModel = photoViewModel ?? throw new ArgumentNullException(nameof(photoViewModel));
            DataContext = photoViewModel;
            InitializeComponent();
            Show();
            Restore();
        }

        [NotNull]
        public IPhoto Photo => _photoViewModel.Photo;

        private void Image_TargetUpdated(object sender, DataTransferEventArgs e)
        {
            ZoomBorder.Reset();
        }
    }
}