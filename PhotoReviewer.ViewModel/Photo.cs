using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Common.Logging;
using GalaSoft.MvvmLight.Messaging;
using JetBrains.Annotations;
using PhotoReviewer.Contracts.DAL.Data;
using PhotoReviewer.Contracts.ViewModel;
using PhotoReviewer.Resources;
using PropertyChanged;
using Scar.Common.Drawing.ImageRetriever;
using Scar.Common.Drawing.Metadata;

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
        private readonly IMessenger _messenger;

        private int? _index;

        public Photo(
            [NotNull] string filePath,
            [NotNull] PhotoUserInfo photoUserInfo,
            [NotNull] PhotoCollection collection,
            [NotNull] ILog logger,
            [NotNull] IMessenger messenger,
            [NotNull] IImageRetriever imageRetriever)
        {
            if (photoUserInfo == null)
                throw new ArgumentNullException(nameof(photoUserInfo));

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            _imageRetriever = imageRetriever ?? throw new ArgumentNullException(nameof(imageRetriever));
            _collection = collection ?? throw new ArgumentNullException(nameof(collection));
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            Favorited = photoUserInfo.Favorited;
            MarkedForDeletion = photoUserInfo.MarkedForDeletion;
            //Set name only - filepath is not changed
            SetFilePathAndName(filePath);
        }

        [DependsOn(nameof(Metadata))]
        public bool OrientationIsSpecified => Metadata.Orientation != Orientation.NotSpecified;

        [DependsOn(nameof(Metadata))]
        public bool DateImageTakenIsSpecified => Metadata.DateImageTaken != null;

        public bool LastOperationFailed { get; set; }

        public bool LastOperationFinished { get; set; }

        [NotNull]
        public ExifMetadata Metadata { get; set; } = EmptyMetadataForInit;

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
                return index == _collection.FilteredView.Count - 1 || index == -1
                    ? null
                    : (Photo) _collection.FilteredView.GetItemAt(index + 1);
            }
        }

        [CanBeNull]
        [DoNotNotify]
        public Photo Prev
        {
            get
            {
                var index = Index;
                return index == 0 || index == -1
                    ? null
                    : (Photo) _collection.FilteredView.GetItemAt(index - 1);
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
                return;

            PositionInCollection = $"{Index + 1} of {_collection.FilteredView.Count}";
        }

        public async Task LoadThumbnailAsync(CancellationToken cancellationToken)
        {
            try
            {
                var thumbnailBytes = Metadata.ThumbnailBytes ?? await _imageRetriever.GetThumbnailAsync(FilePath, cancellationToken).ConfigureAwait(false);
                if (thumbnailBytes != null)
                    Thumbnail = await _imageRetriever.LoadImageAsync(thumbnailBytes, cancellationToken, Metadata.Orientation).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                var message = $"Cannot load thumbnail for {this}";
                _logger.Warn(message, ex);
                _messenger.Send(message, MessengerTokens.UserErrorToken);
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
            return FilePath;
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
                    _collection.MarkedForDeletionCount++;
                else
                    _collection.MarkedForDeletionCount--;
            }
        }

        public bool Favorited
        {
            get => _favorited;
            set
            {
                _favorited = value;
                if (value)
                    _collection.FavoritedCount++;
                else
                    _collection.FavoritedCount--;
            }
        }

        [NotNull]
        public string Name { get; private set; } = string.Empty;

        [NotNull]
        public string FilePath { get; private set; }

        public void SetFilePathAndName([NotNull] string filePath)
        {
            if (FilePath != filePath)
                FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            Name = Path.GetFileNameWithoutExtension(filePath);
        }

        [CanBeNull]
        public BitmapSource Thumbnail { get; set; }

        #endregion
    }
}