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
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));
            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata));
            FilePath = filePath;
            Metadata = metadata;
            MarkedForDeletion = markedForDeletion;
            Favorited = favorited || File.Exists(GetFavoritedFilePath(filePath));
            Name = GetName(filePath);
        }

        [NotNull]
        public string FilePath { get; }

        [NotNull]
        public string Name { get; }

        [NotNull]
        public ExifMetadata Metadata { get; }

        public bool MarkedForDeletion { get; }

        public bool Favorited { get; }

        public static string GetName([NotNull] string filePath)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));
            return Path.GetFileNameWithoutExtension(filePath);
        }

        [NotNull]
        public static string GetFavoriteDirectory([NotNull] string filePath)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));
            var originalDirectory = Path.GetDirectoryName(filePath);
            if (originalDirectory == null)
                throw new InvalidOperationException($"Filepath {filePath} is invalid");
            var favoriteDirectory = Path.Combine(originalDirectory, FavoriteDirectoryName);
            return favoriteDirectory;
        }

        [NotNull]
        private static string GetFileName([NotNull] string filePath)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));
            var name = Path.GetFileName(filePath);
            if (name == null)
                throw new InvalidOperationException($"Filepath {filePath} is invalid");
            return name;
        }

        [NotNull]
        public static string GetFavoritedFilePath([NotNull] string filePath)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));
            return Path.Combine(GetFavoriteDirectory(filePath), GetFileName(filePath));
        }
    }
}