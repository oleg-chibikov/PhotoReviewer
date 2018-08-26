using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Common.Logging;
using Easy.MessageHub;
using JetBrains.Annotations;
using PhotoReviewer.Contracts.DAL;
using PhotoReviewer.Contracts.DAL.Data;
using PhotoReviewer.Contracts.ViewModel;
using PropertyChanged;
using Scar.Common.Drawing.ImageRetriever;
using Scar.Common.Drawing.Metadata;
using Scar.Common.Drawing.MetadataExtractor;
using Scar.Common.Messages;

namespace PhotoReviewer.ViewModel
{
    /// <summary>This class describes a single photo - its location, the image and the metadata extracted from the image.</summary>
    [AddINotifyPropertyChangedInterface]
    [UsedImplicitly]
    public sealed class Photo : IPhoto
    {
        private static readonly ExifMetadata EmptyMetadataForInit = new ExifMetadata();

        [NotNull]
        private readonly PhotoCollection _collection;

        [NotNull]
        private readonly IImageRetriever _imageRetriever;

        [NotNull]
        private readonly ILog _logger;

        [NotNull]
        private readonly IMessageHub _messenger;

        [NotNull]
        public readonly Task LoadAdditionalInfoTask; //TODO: create the proper task during construction

        private int? _index;

        public Photo(
            [NotNull] FileLocation fileLocation,
            [NotNull] PhotoUserInfo photoUserInfo,
            [NotNull] PhotoCollection collection,
            [NotNull] ILog logger,
            [NotNull] IMessageHub messenger,
            [NotNull] IImageRetriever imageRetriever,
            [NotNull] IMetadataExtractor metadataExtractor,
            CancellationToken cancellationToken,
            [NotNull] IPhotoUserInfoRepository photoUserInfoRepository)
        {
            if (photoUserInfo == null)
            {
                throw new ArgumentNullException(nameof(photoUserInfo));
            }

            photoUserInfoRepository = photoUserInfoRepository ?? throw new ArgumentNullException(nameof(photoUserInfoRepository));
            metadataExtractor = metadataExtractor ?? throw new ArgumentNullException(nameof(metadataExtractor));

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            _imageRetriever = imageRetriever ?? throw new ArgumentNullException(nameof(imageRetriever));
            _collection = collection ?? throw new ArgumentNullException(nameof(collection));
            FileLocation = fileLocation ?? throw new ArgumentNullException(nameof(fileLocation));
            Favorited = photoUserInfo.Favorited;
            MarkedForDeletion = photoUserInfo.MarkedForDeletion;
            LoadAdditionalInfoTask = Task.Run(
                async () =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    var favoritedFileExists = File.Exists(FileLocation.FavoriteFilePath);
                    if (!Favorited && favoritedFileExists)
                    {
                        _logger.Info($"Favoriting {this} due to existence of favorited file...");
                        photoUserInfoRepository.Favorite(fileLocation);
                        Favorited = true;
                    }

                    //await Task.Delay(5000, _cancellationToken);
                    Metadata = await metadataExtractor.ExtractAsync(FileLocation.ToString()).ConfigureAwait(false);
                    await LoadThumbnailAsync(cancellationToken).ConfigureAwait(false);
                });
        }

        [DependsOn(nameof(Metadata))]
        public bool OrientationIsSpecified => Metadata.Orientation != Orientation.NotSpecified;

        [DependsOn(nameof(Metadata))]
        public bool DateImageTakenIsSpecified => Metadata.DateImageTaken != null;

        public bool LastOperationFailed { get; set; }

        public bool LastOperationFinished { get; set; }

        [NotNull]
        public ExifMetadata Metadata { get; private set; } = EmptyMetadataForInit;

        [NotNull]
        public string PositionInCollection { get; private set; } = "Not set";

        [DependsOn(nameof(Name), nameof(PositionInCollection))]
        [NotNull]
        public string DisplayedInfo => $"{Name} {Metadata.CameraModel} {PositionInCollection}";

        [DependsOn(nameof(MarkedForDeletion), nameof(Favorited))]
        public bool IsValuable => MarkedForDeletion || Favorited;

        [DoNotNotify]
        private int Index => _index ?? (_index = _collection.FilteredView.IndexOf(this)).Value;

        [CanBeNull]
        [DoNotNotify]
        public Photo Next
        {
            get
            {
                var index = Index;
                return index == _collection.FilteredView.Count - 1 || index == -1 ? null : (Photo)_collection.FilteredView.GetItemAt(index + 1);
            }
        }

        [CanBeNull]
        [DoNotNotify]
        public Photo Prev
        {
            get
            {
                var index = Index;
                return index == 0 || index == -1 ? null : (Photo)_collection.FilteredView.GetItemAt(index - 1);
            }
        }

        /// <summary>
        /// A hack to raise NotifyPropertyChanged for other properties
        /// </summary>
        [AlsoNotifyFor(nameof(Metadata))]
        private bool ReRenderMetadataSwitch { get; set; }

        //TODO: replace by collection event?
        public void ReloadCollectionInfoIfNeeded()
        {
            if (_index != null)
            {
                return;
            }

            PositionInCollection = $"{Index + 1} of {_collection.FilteredView.Count}";
        }

        private async Task LoadThumbnailAsync(CancellationToken cancellationToken)
        {
            try
            {
                var thumbnailBytes = Metadata.ThumbnailBytes ?? await _imageRetriever.GetThumbnailAsync(FileLocation.ToString(), cancellationToken).ConfigureAwait(false);
                Thumbnail = await _imageRetriever.LoadImageAsync(thumbnailBytes, cancellationToken, Metadata.Orientation).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                var message = $"Cannot load thumbnail for {this}";
                _logger.Warn(message, ex);
                _messenger.Publish(message.ToError());
            }
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

        #region Dependency Properties

        private bool _markedForDeletion;
        private bool _favorited;

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

        [NotNull]
        public FileLocation FileLocation { get; set; }

        [NotNull]
        public string Name => FileLocation.FileName;

        [CanBeNull]
        public BitmapSource Thumbnail { get; set; }

        #endregion
    }
}