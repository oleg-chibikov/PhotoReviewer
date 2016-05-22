using System;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using JetBrains.Annotations;
using Application = System.Windows.Application;

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
        private static readonly DependencyProperty PathProperty = DependencyProperty<Photo>.Register(x => x.Path);

        public Photo([NotNull] string path, [NotNull] ExifMetadata metadata, [NotNull] PhotoCollection collection)
        {
            this.collection = collection;
            ChangePath(path);
            MarkedForDeletion = collection.DbProvider.Check(Path, DbProvider.OperationType.MarkForDeletion);
            Favorited = collection.DbProvider.Check(Path, DbProvider.OperationType.Favorite);
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
        public string Path
        {
            get { return (string)GetValue(PathProperty); }
            private set { SetValue(PathProperty, value); }
        }

        [CanBeNull]
        public BitmapSource Image
        {
            get
            {
                var bytes = File.ReadAllBytes(Path);
                var scr = Screen.FromHandle(new WindowInteropHelper(Application.Current.MainWindow).Handle);
                var bitmap = LoadImage(bytes, Metadata.Orientation, scr.WorkingArea.Width / 2);
                GC.Collect();
                return bitmap;
            }
        }

        [CanBeNull]
        public BitmapSource GetFullImage([CanBeNull] EventHandler onCompleted = null)
        {
            var bytes = File.ReadAllBytes(Path);
            var bitmap = LoadImage(bytes, Metadata.Orientation, onCompleted: onCompleted);
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

        public static BitmapSource LoadImage([CanBeNull] byte[] imageData, [CanBeNull] Orientation? orientation = null, int sizeAnchor = 0, [CanBeNull] EventHandler onCompleted = null)
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

            if (orientation == null)
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

        [CanBeNull]
        public string RenameToDate()
        {
            var oldPath = Path;
            if (!File.Exists(Path) || !Metadata.DateImageTaken.HasValue)
                return null;
            var newName = Metadata.DateImageTaken.Value.ToString("yyyy-MM-dd hh-mm-ss");
            if (newName == Name)
                return null;
            var dir = System.IO.Path.GetDirectoryName(Path);
            var newPath = GetFreeFileName($"{dir}\\{newName}.jpg");
            if (!File.Exists(newPath))
                File.Move(oldPath, newPath);
            //FileSystemWatcher will do the rest
            return newPath;
        }

        public void MarkForDeletion()
        {
            if (MarkedForDeletion)
            {
                MarkedForDeletion = false;
                collection.DbProvider.Delete(Path, DbProvider.OperationType.MarkForDeletion);
            }
            else
            {
                MarkedForDeletion = true;
                collection.DbProvider.Save(Path, DbProvider.OperationType.MarkForDeletion);
                Favorited = false;
                collection.DbProvider.Delete(Path, DbProvider.OperationType.Favorite); //clear favorited when marked for deletion
            }
        }

        public void Favorite()
        {
            if (Favorited)
            {
                Favorited = false;
                collection.DbProvider.Delete(Path, DbProvider.OperationType.Favorite);
            }
            else
            {
                Favorited = true;
                collection.DbProvider.Save(Path, DbProvider.OperationType.Favorite);
                MarkedForDeletion = false;
                collection.DbProvider.Delete(Path, DbProvider.OperationType.MarkForDeletion); //clear deleted when favorited
            }
        }

        public void ChangePath([NotNull] string path)
        {
            Path = path;
            Name = System.IO.Path.GetFileNameWithoutExtension(path);
        }

        public override string ToString()
        {
            return Path;
        }

        [NotNull]
        private string GetFreeFileName([NotNull] string fullPath)
        {
            var count = 1;

            var fileNameOnly = System.IO.Path.GetFileNameWithoutExtension(fullPath);
            var extension = System.IO.Path.GetExtension(fullPath);
            var path = System.IO.Path.GetDirectoryName(fullPath);
            if (path == null)
                throw new ArgumentException(nameof(fullPath));
            var newFullPath = fullPath;

            while (File.Exists(newFullPath))
            {
                var tempFileName = $"{fileNameOnly} ({count++})";
                newFullPath = System.IO.Path.Combine(path, tempFileName + extension);
            }
            return newFullPath;
        }
    }
}