using System;
using System.Linq;
using System.Windows.Input;
using Common.Logging;
using JetBrains.Annotations;
using PropertyChanged;
using Scar.Common.WPF.Commands;

namespace PhotoReviewer.ViewModel
{
    [AddINotifyPropertyChangedInterface]
    [UsedImplicitly]
    public sealed class ShiftDateViewModel
    {
        //TODO: Close only dialog on esc
        [NotNull]
        private readonly ILog _logger;

        [NotNull]
        private readonly MainViewModel _mainViewModel;

        [CanBeNull]
        private Photo[] _photos;

        //TODO: recreate model for every dialog call?
        public ShiftDateViewModel([NotNull] MainViewModel mainViewModel, [NotNull] ILog logger)
        {
            _mainViewModel = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            ShiftDateCommand = new CorrelationCommand(ShiftDateAsync);
            CancelShiftDateCommand = new CorrelationCommand(CancelShiftDate);
            OpenShiftDateDialogCommand = new CorrelationCommand(OpenShiftDateDialog);
        }

        public bool IsShiftDateDialogOpen { get; set; }

        [NotNull]
        public ICommand ShiftDateCommand { get; }

        [NotNull]
        public ICommand CancelShiftDateCommand { get; }

        [NotNull]
        public ICommand OpenShiftDateDialogCommand { get; }

        public TimeSpan ShiftBy { get; set; }
        public bool Plus { get; set; } = true;
        public bool RenameToDate { get; set; } = true;
        public int PhotosCount { get; private set; }

        private void CancelShiftDate()
        {
            _logger.Trace("Cancelling shifting date...");
            ShiftBy = default(TimeSpan);
            IsShiftDateDialogOpen = false;
            Plus = true;
        }

        private void OpenShiftDateDialog()
        {
            _logger.Trace("Showing shift date dialog for selected photos...");
            if (!_mainViewModel.SelectedPhotos.Any())
                throw new InvalidOperationException("Photos are not selected");

            IsShiftDateDialogOpen = true;
            PhotosCount = _mainViewModel.SelectedCount;
            _photos = _mainViewModel.SelectedPhotos.ToArray();
        }

        private async void ShiftDateAsync()
        {
            _logger.Info("Shifting date for selected photos...");
            IsShiftDateDialogOpen = false;
            if (_photos == null)
                throw new InvalidOperationException("Photos are not set");

            await _mainViewModel.PhotoCollection.ShiftDateAsync(_photos, ShiftBy, Plus, RenameToDate).ConfigureAwait(false);
        }
    }
}