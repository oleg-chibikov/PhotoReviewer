using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using JetBrains.Annotations;

namespace PhotoReviewer
{
    /// <summary>
    /// This class describes a single photo - its location, the image and
    /// the metadata extracted from the image.
    /// </summary>
    public class Photo : DependencyObject
    {
        [NotNull]
        private readonly PhotoCollection collection;
        [NotNull]
        private static readonly DependencyProperty MarkedForDeletionProperty = DependencyProperty<Photo>.Register(x => x.MarkedForDeletion);
        [NotNull]
        private static readonly DependencyProperty FavoritedProperty = DependencyProperty<Photo>.Register(x => x.Favorited);
        [NotNull]
        private static readonly DependencyProperty NameProperty = DependencyProperty<Photo>.Register(x => x.Name);
        [NotNull]
        private static readonly DependencyProperty SourceProperty = DependencyProperty<Photo>.Register(x => x.Source);
        private static readonly int SizeAnchor = (int)(SystemParameters.FullPrimaryScreenWidth / 1.5);

        public Photo([NotNull] string source, [NotNull] ExifMetadata metadata, [NotNull] PhotoCollection collection)
        {
            this.collection = collection;
            ChangeSource(source);
            MarkedForDeletion = DbProvider.Check(Source, DbProvider.OperationType.MarkForDeletion);
            Favorited = DbProvider.Check(Source, DbProvider.OperationType.Favorite);
            Metadata = metadata;
        }

        [NotNull]
        public ExifMetadata Metadata { get; }

        public bool MarkedForDeletion
        {
            get { return (bool)GetValue(MarkedForDeletionProperty); }
            private set
            {
                SetValue(MarkedForDeletionProperty, value);
                collection.MarkedForDeletionChanged();
            }
        }

        public bool Favorited
        {
            get { return (bool)GetValue(FavoritedProperty); }
            private set
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
        public string Source
        {
            get { return (string)GetValue(SourceProperty); }
            private set { SetValue(SourceProperty, value); }
        }

        [CanBeNull]
        public BitmapSource Image
        {
            get
            {
                var bytes = File.ReadAllBytes(Source);
                var bitmap = LoadImage(bytes, Metadata.Orientation, SizeAnchor);
                GC.Collect();
                return bitmap;
            }
        }

        [CanBeNull]
        public BitmapSource GetFullImage([CanBeNull]EventHandler onCompleted = null)
        {
            var bytes = File.ReadAllBytes(Source);
            var bitmap = LoadImage(bytes, Metadata.Orientation, onCompleted:onCompleted);
            GC.Collect();
            return bitmap;
        }

        [CanBeNull]
        public Photo Next
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
        public Photo Prev
        {
            get
            {
                var index = Index;
                return index == 0 || index == -1
                    ? null
                    : collection[index - 1];
            }
        }

        public int Index => collection.IndexOf(this);

        [NotNull]
        public string PositionInCollection => $"{Index + 1} of {collection.Count}";

        public static BitmapSource LoadImage([CanBeNull] byte[] imageData, [CanBeNull] Orientation? orientation = null, int sizeAnchor = 0, [CanBeNull]EventHandler onCompleted = null)
        {
            if (imageData == null || imageData.Length == 0)
                return null;
            var image = new BitmapImage();
            using (var mem = new MemoryStream(imageData))
            {
                mem.Position = 0;
                image.BeginInit();
                image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                image.CacheOption = BitmapCacheOption.OnLoad;
                if (sizeAnchor > 0)
                    image.DecodePixelWidth = sizeAnchor;
                image.UriSource = null;
                image.StreamSource = mem;
                image.Changed += onCompleted;
                image.EndInit();
            }

            image.Freeze();

            if (orientation==null)
                return image;

            var angle = 0;
            switch (orientation)
            {
                case Orientation.Straight:
                    break;
                case Orientation.Left:
                    angle = 90;
                    break;
                case Orientation.Right:
                    angle = 270;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(orientation), orientation, null);
            }
            if (angle == 0)
                return image;

            var tb = new TransformedBitmap();
            tb.BeginInit();
            tb.Source = image;
            var transform = new System.Windows.Media.RotateTransform(angle);
            tb.Transform = transform;
            tb.EndInit();

            tb.Freeze();
            return tb;
        }
        
        public void MarkForDeletion()
        {
            if (MarkedForDeletion)
            {
                MarkedForDeletion = false;
                DbProvider.Delete(Source, DbProvider.OperationType.MarkForDeletion);
            }
            else
            {
                MarkedForDeletion = true;
                DbProvider.Save(Source, DbProvider.OperationType.MarkForDeletion);
                Favorited = false;
                DbProvider.Delete(Source, DbProvider.OperationType.Favorite); //clear favorited when marked for deletion
            }
        }

        public void Favorite()
        {
            if (Favorited)
            {
                Favorited = false;
                DbProvider.Delete(Source, DbProvider.OperationType.Favorite);
            }
            else
            {
                Favorited = true;
                DbProvider.Save(Source, DbProvider.OperationType.Favorite);
                MarkedForDeletion = false;
                DbProvider.Delete(Source, DbProvider.OperationType.MarkForDeletion); //clear deleted when favorited
            }
        }

        public void ChangeSource([NotNull] string source)
        {
            Source = source;
            Name = Path.GetFileNameWithoutExtension(source);
        }
        
        public override string ToString()
        {
            return Source;
        }
    }
}