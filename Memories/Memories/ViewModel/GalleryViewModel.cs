using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using PhotoReviewer.Memories.DAL;
using PhotoReviewer.Memories.Data;
using PhotoReviewer.Memories.Utils;
using PropertyChanged;
using Scar.Common.ImageProcessing.MetadataExtraction;
using Scar.Common.MVVM.Commands;
using Scar.Common.MVVM.ViewModel;

namespace PhotoReviewer.Memories.ViewModel;

public class GalleryViewModel : BaseViewModel
{
    readonly IFileRecordRepositoryFactory _fileRecordRepositoryFactory;
    readonly IUiThreadRunner _uiThreadRunner;
    readonly IMetadataExtractor _metadataExtractor;
    readonly BackgroundWorker _backgroundWorker = new();
    GranularityType _selectedGranularity = GranularityType.Week;
    int _imagesToShow = 50;
    IReadOnlyCollection<string> _imagePaths = Array.Empty<string>();

    public GalleryViewModel(
        ICommandManager commandManager,
        IFileRecordRepositoryFactory fileRecordRepositoryFactory,
        IUiThreadRunner uiThreadRunner,
        IMetadataExtractor metadataExtractor) : base(commandManager)
    {
        _fileRecordRepositoryFactory = fileRecordRepositoryFactory ??
                                       throw new ArgumentNullException(nameof(fileRecordRepositoryFactory));
        _uiThreadRunner = uiThreadRunner ?? throw new ArgumentNullException(nameof(uiThreadRunner));
        _metadataExtractor = metadataExtractor ?? throw new ArgumentNullException(nameof(metadataExtractor));
        LoadImagesCommand = AddCommand<int>(LoadImagesAsync, debugName: nameof(LoadImagesAsync));
        LoadedCommand = AddCommand(HandleLoadedAsync, debugName: nameof(HandleLoadedAsync));
        OpenPhotoCommand = AddCommand(OpenPhotoAsync, debugName: nameof(OpenPhotoAsync));
        _backgroundWorker.WorkerSupportsCancellation = true;
        _backgroundWorker.DoWork += LoadActualImages;
    }

    public event EventHandler? ScrollRequested;

    [DependsOn(
        nameof(IsLoading),
        nameof(NoItems))]
    public bool ShowItems => !IsLoading && !NoItems;

    public bool IsLoading { get; set; }

    public bool NoItems { get; set; }

    public GranularityType SelectedGranularity
    {
        get => _selectedGranularity;
        set
        {
            if (_selectedGranularity == value)
            {
                return;
            }

            _selectedGranularity = value;
            OnPropertyChanged(nameof(SelectedGranularity));

            LoadImagesAsync(CurrentYear).ConfigureAwait(false);
        }
    }

    public int ImagesToShow
    {
        get => _imagesToShow;
        set
        {
            if (_imagesToShow == value)
            {
                return;
            }

            _imagesToShow = value;
            OnPropertyChanged(nameof(ImagesToShow));

            LoadImagesAsync(CurrentYear).ConfigureAwait(false);
        }
    }

    public ObservableCollection<int> YearsCollection { get; } = new();

    public ObservableCollection<BitmapImage> Images { get; } = new();

    public string Title { get; private set; } = string.Empty;

    public ICommand LoadImagesCommand { get; }

    public ICommand LoadedCommand { get; }

    public ICommand OpenPhotoCommand { get; }

    public int CurrentYear { get; set; }

    public async Task LoadYearsAsync()
    {
        await Task.Run(
            () =>
            {
                // Fetch the smallest and largest years from the database// Retrieve the years from the repository and fill the YearsCollection
                var yearRange = _fileRecordRepositoryFactory.GetYears();
                if (yearRange == null)
                {
                    return;
                }

                _uiThreadRunner.Run(
                    () =>
                    {
                        foreach (var year in Enumerable.Range(
                                     yearRange.MinYear,
                                     yearRange.MaxYear - yearRange.MinYear + 1))
                        {
                            YearsCollection.Add(year);
                        }
                    });
            }).ConfigureAwait(false);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _backgroundWorker.DoWork -= LoadActualImages;
            _backgroundWorker.Dispose();
        }
    }

    static IEnumerable<BitmapImage> LoadPlaceholderImages(int count)
    {
        return Enumerable.Range(
            0,
            count).Select(
            _ => new BitmapImage(new Uri("pack://application:,,,/PhotoReviewer.Memories;component/Placeholder.jpg")));
    }

    async Task<BitmapImage> LoadImageFileAsync(string filePath, int decodePixelWidth = 1200)
    {
        var metadata = await _metadataExtractor.ExtractAsync(filePath, MetadataOptions.Orientation).ConfigureAwait(false);

        var image = new BitmapImage();
        image.BeginInit();
        image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = decodePixelWidth;
        image.UriSource = new Uri(filePath);
        image.Rotation = metadata.Orientation.ToRotation();
        image.EndInit();
        image.Freeze();

        return image;
    }

    async Task HandleLoadedAsync()
    {
        await RunWithLoadingAsync(
            async () =>
            {
                await Task.WhenAll(
                    LoadYearsAsync(),
                    LoadImagesCoreAsync(DateTime.Today.Year - 1)).ConfigureAwait(false);
            }).ConfigureAwait(false);
    }

    async Task LoadImagesAsync(int year)
    {
        await RunWithLoadingAsync(
            async () =>
            {
                await LoadImagesCoreAsync(year).ConfigureAwait(false);
            }).ConfigureAwait(false);
    }

    async Task LoadImagesCoreAsync(int year)
    {
        await Task.Run(
            async () =>
            {
                _backgroundWorker.CancelAsync();
                _uiThreadRunner.Run(() =>
                {
                    NoItems = false;
                    ScrollRequested?.Invoke(this, EventArgs.Empty);
                    SetCurrentYear(year);
                });

                var startOfPeriod = GetStartOfPeriod(year);
                var endOfPeriod = GetEndOfPeriod(startOfPeriod);

                _imagePaths = GetImagePaths(
                    startOfPeriod,
                    endOfPeriod);

                var images = LoadPlaceholderImages(_imagePaths.Count);

                while (_backgroundWorker.IsBusy)
                {
                    await Task.Delay(50).ConfigureAwait(false);
                }

                _uiThreadRunner.Run(
                    () =>
                    {
                        UpdateUiWithNewImages(
                            images,
                            startOfPeriod,
                            endOfPeriod);
                        NoItems = _imagePaths.Count == 0;
                    });

                if (!NoItems)
                {
                    // Load actual images instead of placeholders
                    _backgroundWorker.RunWorkerAsync();
                }

            }).ConfigureAwait(false);
    }

    void LoadActualImages(object? o, DoWorkEventArgs e)
    {
        var i = 0;
        foreach (var filePath in _imagePaths)
        {
            if (_backgroundWorker.CancellationPending)
            {
                e.Cancel = true;
                return;
            }

            var image = LoadImageFileAsync(filePath).Result;

            _uiThreadRunner.Run(
                () =>
                {
                    Images[i++] = image;
                });
        }
    }

    void OpenPhotoAsync()
    {
         // TODO: Extract reusable part from Photo and PhotoVM, decouple them from MainViewModel and reuse here
                 //   var photo = _photoFactory(fileLocation, photoUserInfo, cancellationToken, this);
    }

    void SetCurrentYear(int year)
    {
        CurrentYear = year;
    }

    DateTime GetStartOfPeriod(int year)
    {
        var today = DateTime.Today;
        return SelectedGranularity switch
        {
            GranularityType.Day => new DateTime(year, today.Month, today.Day),
            GranularityType.Week => new DateTime(year, today.Month, today.Day).StartOfWeek(),
            GranularityType.Month => new DateTime(year, today.Month, 1),
            _ => throw new NotSupportedException(nameof(SelectedGranularity))
        };
    }

    DateTime GetEndOfPeriod(DateTime startOfPeriod)
    {
        return SelectedGranularity switch
        {
            GranularityType.Day => startOfPeriod.AddDays(1),
            GranularityType.Week => startOfPeriod.AddDays(7),
            GranularityType.Month => startOfPeriod.AddMonths(1),
            _ => throw new NotSupportedException(nameof(SelectedGranularity))
        };
    }

    List<string> GetImagePaths(DateTime startOfPeriod, DateTime endOfPeriod)
    {
        return _fileRecordRepositoryFactory.GetRecords(startOfPeriod, endOfPeriod, ImagesToShow == 100500 ? null : ImagesToShow).Select(x => x.Id).ToList();
    }

    void UpdateUiWithNewImages(IEnumerable<BitmapImage> images, DateTime startOfPeriod, DateTime endOfPeriod)
    {
        Images.Clear();
        foreach (var image in images)
        {
            Images.Add(image);
        }

        Title = SelectedGranularity switch
        {
            GranularityType.Day => $"Memories from {startOfPeriod:dd MMM yyyy}",
            GranularityType.Month => $"Memories from {startOfPeriod:MMM yyyy}",
            GranularityType.Week =>
                // Handle week case here
                $"Memories for the week {startOfPeriod:dd MMM yyyy} - {endOfPeriod:dd MMM yyyy}",
            _ => "Memories"
        };
    }

    async Task RunWithLoadingAsync(Func<Task> action)
    {
        _uiThreadRunner.Run(() => IsLoading = true); // Set loading to true before loading

        try
        {
            await action().ConfigureAwait(false);
        }
        finally
        {
            _uiThreadRunner.Run(() => IsLoading = false); // Set loading to false after loading
        }
    }
}
