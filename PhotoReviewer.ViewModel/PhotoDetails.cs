using System;
using System.IO;
using JetBrains.Annotations;
using Scar.Common.Drawing.Metadata;

namespace PhotoReviewer.ViewModel
{
    public sealed class PhotoDetails
    {
        private const string FavoriteDirectoryName = "Favorite";

        internal PhotoDetails([NotNull] string filePath, [NotNull] ExifMetadata metadata, bool markedForDeletion,
            bool favorited)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            MarkedForDeletion = markedForDeletion;
            Favorited = favorited || File.Exists(GetFavoritedFilePath(filePath));
        }

        [NotNull]
        internal string FilePath { get; }

        [NotNull]
        internal ExifMetadata Metadata { get; }

        internal bool MarkedForDeletion { get; }

        internal bool Favorited { get; }

        [NotNull]
        internal static string GetFavoriteDirectory([NotNull] string filePath)
        {
            var originalDirectory = Path.GetDirectoryName(filePath);
            // ReSharper disable once AssignNullToNotNullAttribute
            var favoriteDirectory = Path.Combine(originalDirectory, FavoriteDirectoryName);
            return favoriteDirectory;
        }

        [NotNull]
        internal static string GetFavoritedFilePath([NotNull] string filePath)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            return Path.Combine(GetFavoriteDirectory(filePath), Path.GetFileName(filePath));
        }
    }
}