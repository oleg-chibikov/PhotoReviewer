using System;
using System.IO;
using JetBrains.Annotations;
using Scar.Common.Drawing.Metadata;

namespace PhotoReviewer.ViewModel
{
    public sealed class PhotoDetails
    {
        private const string FavoriteDirectoryName = "Favorite";

        public PhotoDetails([NotNull] string filePath, [NotNull] ExifMetadata metadata, bool markedForDeletion, bool favorited)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            MarkedForDeletion = markedForDeletion;
            Favorited = favorited || File.Exists(GetFavoritedFilePath(filePath));
        }

        [NotNull]
        public string FilePath { get; }

        [NotNull]
        public ExifMetadata Metadata { get; }

        public bool MarkedForDeletion { get; }

        public bool Favorited { get; }

        [NotNull]
        public static string GetFavoritedFilePath([NotNull] string filePath)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            return Path.Combine(GetFavoriteDirectory(filePath), Path.GetFileName(filePath));
        }

        [NotNull]
        public static string GetFavoriteDirectory([NotNull] string filePath)
        {
            var originalDirectory = Path.GetDirectoryName(filePath);
            // ReSharper disable once AssignNullToNotNullAttribute
            var favoriteDirectory = Path.Combine(originalDirectory, FavoriteDirectoryName);
            return favoriteDirectory;
        }
    }
}