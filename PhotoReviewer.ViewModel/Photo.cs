using System;
using System.IO;
using System.Threading;
using System.Windows.Media;
using JetBrains.Annotations;
using Microsoft.VisualBasic.FileIO;
using PropertyChanged;
using Scar.Common.Drawing;
using Scar.Common.Drawing.Data;

namespace PhotoReviewer.ViewModel
{
    /// <summary>
    /// This class describes a single photo - its location, the image and
    /// the metadata extracted from the image.
    /// </summary>
    [ImplementPropertyChanged]
    public class Photo
    {
        [NotNull]
        private readonly PhotoCollection collection;

        public Photo([NotNull] PhotoDetails photoDetails, [NotNull] PhotoCollection collection, CancellationToken cancellationToken)
        {
            if (photoDetails == null)
                throw new ArgumentNullException(nameof(photoDetails));
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));
            this.collection = collection;
            filePath = photoDetails.FilePath;
            Name = photoDetails.Name;
            MarkedForDeletion = photoDetails.MarkedForDeletion;
            Favorited = photoDetails.Favorited;
            Metadata = photoDetails.Metadata;
            SetThumbnailAsync(cancellationToken);
        }

        [NotNull]
        public ExifMetadata Metadata { get; set; }

        public bool IsValuableOrNearby => IsValuable || RealNext?.IsValuable == true || RealPrev?.IsValuable == true;

        [CanBeNull]
        public Photo Next
        {
            get
            {
                if (!collection.ShowOnlyMarked)
                    return RealNext;
                for (var i = Index + 1; i < collection.Count; i++)
                {
                    var current = collection[i];
                    if (current.IsValuableOrNearby)
                        return current;
                }
                return null;
            }
        }

        [CanBeNull]
        public Photo Prev
        {
            get
            {
                if (!collection.ShowOnlyMarked)
                    return RealPrev;
                for (var i = Index - 1; i >= 0; i--)
                {
                    var current = collection[i];
                    if (current.IsValuableOrNearby)
                        return current;
                }
                return null;
            }
        }

        [NotNull]
        public string PositionInCollection => $"{Index + 1} of {collection.Count}";

        [NotNull]
        public string DisplayedInfo => $"{Name} {Metadata.CameraModel} {PositionInCollection}";

        private bool IsValuable => MarkedForDeletion || Favorited;

        private int Index => collection.IndexOf(this);

        [CanBeNull]
        private Photo RealNext
        {
            get
            {
                var index = Index;
                return index == collection.Count - 1 || index == -1
                    ? null
                    : collection[index + 1];
            }
        }

        [CanBeNull]
        private Photo RealPrev
        {
            get
            {
                var index = Index;
                return index == 0 || index == -1
                    ? null
                    : collection[index - 1];
            }
        }

        public async void SetThumbnailAsync(CancellationToken cancellationToken)
        {
            var thumbnailBytes = Metadata.ThumbnailBytes ?? await FilePath.GetThumbnailAsync(cancellationToken);
            try
            {
                Thumbnail = await thumbnailBytes.LoadImageAsync(cancellationToken, Metadata.Orientation);
            }
            catch (OperationCanceledException)
            {
            }
        }

        public override string ToString()
        {
            return FilePath;
        }

        [NotifyPropertyChangedInvocator]
        public void OnCollectionChanged()
        {
            //TODO: May simplify? investigate
            // ReSharper disable ExplicitCallerInfoArgument
            //RaisePropertyChanged(nameof(DisplayedInfo));
            //RaisePropertyChanged(nameof(PositionInCollection));
            // ReSharper restore ExplicitCallerInfoArgument
        }

        public bool DeleteFileIfMarked()
        {
            if (!markedForDeletion)
                return false;
            if (File.Exists(filePath))
                FileSystem.DeleteFile(filePath,
                    UIOption.OnlyErrorDialogs,
                    RecycleOption.SendToRecycleBin);
            return true;
        }

        public bool CopyFileIfFavorited()
        {
            if (!favorited || !File.Exists(filePath))
                return false;
            var favoritedFilePath = PhotoDetails.GetFavoritedFilePath(filePath);
            if (!File.Exists(favoritedFilePath))
                File.Copy(filePath, favoritedFilePath);
            return true;
        }

        #region Dependency Properties

        private bool markedForDeletion;
        private bool favorited;
        private string filePath;

        public bool MarkedForDeletion
        {
            get { return markedForDeletion; }
            set
            {
                markedForDeletion = value;
                if (value)
                    collection.MarkedForDeletionCount++;
                else
                    collection.MarkedForDeletionCount--;
            }
        }

        public bool Favorited
        {
            get { return favorited; }
            set
            {
                favorited = value;
                if (value)
                    collection.FavoritedCount++;
                else
                    collection.FavoritedCount--;
            }
        }

        [NotNull]
        public string Name { get; set; }

        [NotNull]
        public string FilePath
        {
            get { return filePath; }
            set
            {
                filePath = value;
                Name = PhotoDetails.GetName(value);
            }
        }

        [CanBeNull]
        public ImageSource Thumbnail { get; set; }

        #endregion
    }
}