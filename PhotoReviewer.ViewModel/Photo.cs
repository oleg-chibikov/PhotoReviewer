using System;
using System.IO;
using System.Threading;
using System.Windows.Media;
using Common.Logging;
using GalaSoft.MvvmLight.Messaging;
using JetBrains.Annotations;
using Microsoft.VisualBasic.FileIO;
using PhotoReviewer.Contracts.ViewModel;
using PhotoReviewer.Resources;
using PropertyChanged;
using Scar.Common.Drawing.ImageRetriever;
using Scar.Common.Drawing.Metadata;

namespace PhotoReviewer.ViewModel
{
    /// <summary>This class describes a single photo - its location, the image and the metadata extracted from the image.</summary>
    [ImplementPropertyChanged]
    public sealed class Photo : IPhoto
    {
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
            [NotNull] PhotoDetails photoDetails,
            [NotNull] PhotoCollection collection,
            CancellationToken cancellationToken,
            [NotNull] ILog logger,
            [NotNull] IMessenger messenger,
            [NotNull] IImageRetriever imageRetriever)
        {
            if (photoDetails == null)
                throw new ArgumentNullException(nameof(photoDetails));

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            _imageRetriever = imageRetriever ?? throw new ArgumentNullException(nameof(imageRetriever));
            _collection = collection ?? throw new ArgumentNullException(nameof(collection));
            FilePath = photoDetails.FilePath;
            Name = Path.GetFileNameWithoutExtension(FilePath);
            MarkedForDeletion = photoDetails.MarkedForDeletion;
            Favorited = photoDetails.Favorited;
            Metadata = photoDetails.Metadata;
            SetThumbnailAsync(cancellationToken);
        }

        [DependsOn(nameof(Metadata))]
        public bool OrientationIsSpecified => Metadata.Orientation != Orientation.NotSpecified;

        [DependsOn(nameof(Metadata))]
        public bool DateImageTakenIsSpecified => Metadata.DateImageTaken != null;

        public bool LastOperationFailed { get; set; }
        public bool LastOperationFinished { get; set; }

        [NotNull]
        public ExifMetadata Metadata { get; set; }

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

        //TODO: replace by collection event?
        public void ReloadCollectionInfoIfNeeded()
        {
            if (_index != null)
                return;

            PositionInCollection = $"{Index + 1} of {_collection.FilteredView.Count}";
        }

        public void CopyFileIfFavorited()
        {
            if (!_favorited || !File.Exists(FilePath))
                return;

            var favoritedFilePath = PhotoDetails.GetFavoritedFilePath(FilePath);
            if (!File.Exists(favoritedFilePath))
                File.Copy(FilePath, favoritedFilePath);
        }

        public void DeleteFileIfMarked()
        {
            if (!_markedForDeletion || !File.Exists(FilePath))
                return;

            FileSystem.DeleteFile(FilePath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
        }

        public void MarkAsNotSynced()
        {
            _index = null;
        }

        /// <summary>Hack to reload metadata since its properties are not dependency properties</summary>
        public void ReloadMetadata()
        {
            var metadata = Metadata;
            // ReSharper disable once AssignNullToNotNullAttribute
            Metadata = null;
            Metadata = metadata;
        }

        public async void SetThumbnailAsync(CancellationToken cancellationToken)
        {
            try
            {
                var thumbnailBytes = Metadata.ThumbnailBytes ?? await _imageRetriever.GetThumbnailAsync(FilePath, cancellationToken);
                if (thumbnailBytes != null)
                    Thumbnail = await _imageRetriever.LoadImageAsync(thumbnailBytes, cancellationToken, Metadata.Orientation);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                var message = $"Cannot load thumbnail for {FilePath}";
                _logger.Warn(message, ex);
                _messenger.Send(message, MessengerTokens.UserErrorToken);
            }
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
        public string Name { get; set; }

        [NotNull]
        public string FilePath { get; set; }

        public void SetFilePathAndName([NotNull] string filePath)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            Name = Path.GetFileNameWithoutExtension(filePath);
        }

        [CanBeNull]
        public ImageSource Thumbnail { get; set; }

        #endregion
    }
}