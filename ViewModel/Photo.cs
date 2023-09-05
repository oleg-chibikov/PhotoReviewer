using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Easy.MessageHub;
using Microsoft.Extensions.Logging;
using PhotoReviewer.Contracts.DAL;
using PhotoReviewer.Contracts.DAL.Data;
using PhotoReviewer.Contracts.ViewModel;
using PropertyChanged;
using Scar.Common.ImageProcessing.Metadata;
using Scar.Common.ImageProcessing.MetadataExtraction;
using Scar.Common.Messages;
using Scar.Common.WPF.ImageRetrieval;

namespace PhotoReviewer.ViewModel
{
    /// <summary>This class describes a single photo - its location, the image and the metadata extracted from the image.</summary>
    [AddINotifyPropertyChangedInterface]

    public partial class Photo : IPhoto
    {
        static readonly ExifMetadata EmptyMetadataForInit = new ExifMetadata();
        readonly PhotoCollection _collection;
        readonly IImageRetriever _imageRetriever;
        readonly ILogger _logger;
        readonly IMessageHub _messenger;
        readonly IMetadataExtractor _metadataExtractor;
        readonly IPhotoUserInfoRepository _photoUserInfoRepository;
        int? _index;
        bool _markedForDeletion;
        bool _favorited;
        bool _loaded;
        bool _isLoading;

        public Photo(
            FileLocation fileLocation,
            PhotoUserInfo photoUserInfo,
            PhotoCollection collection,
            ILogger<Photo> logger,
            IMessageHub messenger,
            IImageRetriever imageRetriever,
            IMetadataExtractor metadataExtractor,
            IPhotoUserInfoRepository photoUserInfoRepository)
        {
            if (photoUserInfo == null)
            {
                throw new ArgumentNullException(nameof(photoUserInfo));
            }

            _photoUserInfoRepository = photoUserInfoRepository ?? throw new ArgumentNullException(nameof(photoUserInfoRepository));
            _metadataExtractor = metadataExtractor ?? throw new ArgumentNullException(nameof(metadataExtractor));

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            _imageRetriever = imageRetriever ?? throw new ArgumentNullException(nameof(imageRetriever));
            _collection = collection ?? throw new ArgumentNullException(nameof(collection));
            FileLocation = fileLocation ?? throw new ArgumentNullException(nameof(fileLocation));
            Favorited = photoUserInfo.Favorited;
            MarkedForDeletion = photoUserInfo.MarkedForDeletion;
        }

        [DependsOn(nameof(Metadata))]
        public bool OrientationIsSpecified => Metadata.Orientation != Orientation.NotSpecified;

        [DependsOn(nameof(Metadata))]
        public bool DateImageTakenIsSpecified => Metadata.DateImageTaken != null;

        public bool LastOperationFailed { get; set; }

        public bool LastOperationFinished { get; set; }

        public ExifMetadata Metadata { get; set; } = EmptyMetadataForInit;

        public string PositionInCollection { get; set; } = "Not set";

        [DependsOn(nameof(Name), nameof(PositionInCollection))]

        public string DisplayedInfo => $"{Name} {Metadata.CameraModel} {PositionInCollection}";

        [DependsOn(nameof(MarkedForDeletion), nameof(Favorited))]
        public bool IsValuable => MarkedForDeletion || Favorited;

        [DoNotNotify]
        public Photo? Next
        {
            get
            {
                var index = Index;
                return index == _collection.FilteredView.Count - 1 || index == -1 ? null : (Photo)_collection.FilteredView.GetItemAt(index + 1);
            }
        }

        [DoNotNotify]
        public Photo? Prev
        {
            get
            {
                var index = Index;
                return index == 0 || index == -1 ? null : (Photo)_collection.FilteredView.GetItemAt(index - 1);
            }
        }

        public bool MarkedForDeletion
        {
            get => _markedForDeletion;
            set
            {
                _markedForDeletion = value;
                if (value)
                {
                    _collection.MarkedForDeletionCount++;
                }
                else
                {
                    _collection.MarkedForDeletionCount--;
                }
            }
        }

        public bool Favorited
        {
            get => _favorited;
            set
            {
                _favorited = value;
                if (value)
                {
                    _collection.FavoritedCount++;
                }
                else
                {
                    _collection.FavoritedCount--;
                }
            }
        }

        public FileLocation FileLocation { get; set; }

        public string Name => FileLocation.FileName;

        public BitmapSource? Thumbnail { get; set; }

        [DoNotNotify]
        int Index => _index ?? (_index = _collection.FilteredView.IndexOf(this)).Value;

        /// <summary>
        /// A hack to raise NotifyPropertyChanged for other properties.
        /// </summary>
        [AlsoNotifyFor(nameof(Metadata))]
        bool ReRenderMetadataSwitch { get; set; }

        // TODO: replace by collection event?
        public void ReloadCollectionInfoIfNeeded()
        {
            if (_index != null)
            {
                return;
            }

            PositionInCollection = $"{Index + 1} of {_collection.FilteredView.Count}";
        }

        public void MarkAsNotSynced()
        {
            _index = null;
        }

        public void ReloadMetadata()
        {
            ReRenderMetadataSwitch = !ReRenderMetadataSwitch;
        }

        public override string ToString()
        {
            return FileLocation.ToString();
        }

        public async Task LoadAdditionalInfoAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var favoritedFileExists = File.Exists(FileLocation.FavoriteFilePath);
            if (!Favorited && favoritedFileExists)
            {
                _logger.LogInformation("Favoriting {Photo} due to existence of favorited file...", this);
                _photoUserInfoRepository.Favorite(FileLocation);
                Favorited = true;
            }

            // await Task.Delay(5000, _cancellationToken);
            Metadata = await _metadataExtractor.ExtractAsync(FileLocation.ToString()).ConfigureAwait(false);
            await LoadThumbnailAsync(cancellationToken).ConfigureAwait(false);
        }

        async Task LoadThumbnailAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (_loaded || _isLoading)
                {
                    return;
                }

                _isLoading = true;
                var thumbnailBytes = Metadata.ThumbnailBytes ?? await _imageRetriever.GetThumbnailAsync(FileLocation.ToString(), cancellationToken).ConfigureAwait(false);
                Thumbnail = await _imageRetriever.LoadImageAsync(thumbnailBytes, cancellationToken, Metadata.Orientation).ConfigureAwait(false);
                _loaded = true;
                _isLoading = false;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cannot load thumbnail for {Photo}", this);
                _messenger.Publish($"Cannot load thumbnail for {this}".ToError());
            }
        }
    }
}
