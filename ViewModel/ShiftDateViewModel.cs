using System.Windows.Input;
using Microsoft.Extensions.Logging;
using PropertyChanged;
using Scar.Common.MVVM.Commands;

namespace PhotoReviewer.ViewModel;

[AddINotifyPropertyChangedInterface]

// TODO: Close only dialog on esc
public partial class ShiftDateViewModel
{
    readonly ILogger _logger;

    readonly MainViewModel _mainViewModel;

#pragma warning disable SA1011 // Closing square brackets should be spaced correctly
    Photo[]? _photos;
#pragma warning restore SA1011 // Closing square brackets should be spaced correctly

    // TODO: recreate model for every dialog call?
    public ShiftDateViewModel(MainViewModel mainViewModel, ILogger<ShiftDateViewModel> logger, ICommandManager commandManager)
    {
        _mainViewModel = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ShiftDateCommand = new CorrelationCommand(commandManager, ShiftDateAsync);
        CancelShiftDateCommand = new CorrelationCommand(commandManager, CancelShiftDate);
        OpenShiftDateDialogCommand = new CorrelationCommand(commandManager, OpenShiftDateDialog);
    }

    public bool IsShiftDateDialogOpen { get; set; }

    public ICommand ShiftDateCommand { get; }

    public ICommand CancelShiftDateCommand { get; }

    public ICommand OpenShiftDateDialogCommand { get; }

    public TimeSpan ShiftBy { get; set; }

    public bool Plus { get; set; } = true;

    public bool RenameToDate { get; set; } = true;

    public int PhotosCount { get; set; }

    void CancelShiftDate()
    {
        _logger.LogTrace("Cancelling shifting date...");
        ShiftBy = default(TimeSpan);
        IsShiftDateDialogOpen = false;
        Plus = true;
    }

    void OpenShiftDateDialog()
    {
        _logger.LogTrace("Showing shift date dialog for selected photos...");
        if (!_mainViewModel.SelectedPhotos.Any())
        {
            throw new InvalidOperationException("Photos are not selected");
        }

        IsShiftDateDialogOpen = true;
        PhotosCount = _mainViewModel.SelectedCount;
        _photos = _mainViewModel.SelectedPhotos.ToArray();
    }

    async void ShiftDateAsync()
    {
        _logger.LogInformation("Shifting date for selected photos...");
        IsShiftDateDialogOpen = false;
        if (_photos == null)
        {
            throw new InvalidOperationException("Photos are not set");
        }

        await _mainViewModel.PhotoCollection.ShiftDateAsync(_photos, ShiftBy, Plus, RenameToDate).ConfigureAwait(true);
    }
}