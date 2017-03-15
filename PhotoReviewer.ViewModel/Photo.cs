using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using JetBrains.Annotations;
using Scar.Common.Drawing;
using Scar.Common.Drawing.Data;

namespace PhotoReviewer.ViewModel
{
    /// <summary>
    /// This class describes a single photo - its location, the image and
    /// the metadata extracted from the image.
    /// </summary>
    public class Photo : DependencyObject, INotifyPropertyChanged
    {
        [NotNull]
        private static readonly DependencyProperty MarkedForDeletionProperty = DependencyProperty<Photo>.Register(x => x.MarkedForDeletion);

        [NotNull]
        private static readonly DependencyProperty FavoritedProperty = DependencyProperty<Photo>.Register(x => x.Favorited);

        [NotNull]
        private static readonly DependencyProperty NameProperty = DependencyProperty<Photo>.Register(x => x.Name);

        [NotNull]
        private static readonly DependencyProperty FilePathProperty = DependencyProperty<Photo>.Register(x => x.FilePath);

        [NotNull]
        private static readonly DependencyProperty ThumbnailProperty = DependencyProperty<Photo>.Register(x => x.Thumbnail);

        [NotNull]
        private readonly PhotoCollection collection;

        public Photo([NotNull] string path, [NotNull] ExifMetadata metadata, bool markedForDeletion, bool favorited, [NotNull] PhotoCollection collection, CancellationToken cancellationToken)
        {
            this.collection = collection;
            ChangePath(path);
            MarkedForDeletion = markedForDeletion;
            Favorited = favorited;
            Metadata = metadata;
            SetThumbnailAsync(cancellationToken);
        }

        [NotNull]
        public ExifMetadata Metadata { get; }

        public bool MarkedForDeletion
        {
            get { return (bool)GetValue(MarkedForDeletionProperty); }
            set
            {
                SetValue(MarkedForDeletionProperty, value);
                collection.MarkedForDeletionChanged();
            }
        }

        public bool Favorited
        {
            get { return (bool)GetValue(FavoritedProperty); }
            set
            {
                SetValue(FavoritedProperty, value);
                collection.FavoritedChanged();
            }
        }

        [NotNull]
        public string Name
        {
            get { return (string)GetValue(NameProperty); }
            private set { SetValue(NameProperty, value); }
        }

        [NotNull]
        public string FilePath
        {
            get { return (string)GetValue(FilePathProperty); }
            private set { SetValue(FilePathProperty, value); }
        }

        [CanBeNull]
        public ImageSource Thumbnail
        {
            get { return (ImageSource)GetValue(ThumbnailProperty); }
            private set { SetValue(ThumbnailProperty, value); }
        }

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

        [CanBeNull]
        public event PropertyChangedEventHandler PropertyChanged;

        private async void SetThumbnailAsync(CancellationToken cancellationToken)
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

        public void ChangePath([NotNull] string path)
        {
            FilePath = path;
            Name = Path.GetFileNameWithoutExtension(path);
        }

        public override string ToString()
        {
            return FilePath;
        }

        [NotifyPropertyChangedInvocator]
        public void OnCollectionChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayedInfo)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PositionInCollection)));
        }
    }
}