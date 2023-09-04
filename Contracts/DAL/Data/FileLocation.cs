using System;
using System.IO;

namespace PhotoReviewer.Contracts.DAL.Data
{
    public sealed class FileLocation : IEquatable<FileLocation>
    {
        const string FavoriteDirectoryName = "Favorite";

        string? _filePath;

        public FileLocation()
        {
            Directory = string.Empty;
            FileName = string.Empty;
            Extension = string.Empty;
        }

        public FileLocation(string filePath)
        {
            Directory = Path.GetDirectoryName(filePath) ?? throw new ArgumentNullException(nameof(filePath));
            FileName = Path.GetFileNameWithoutExtension(filePath);
            Extension = Path.GetExtension(filePath);
        }

        public string Directory { get; set; }

        public string FileName { get; set; }

        public string Extension { get; set; }

        public string FavoriteDirectory => Path.Combine(Directory, FavoriteDirectoryName);

        public string FavoriteFilePath => Path.Combine(FavoriteDirectory, FileName + Extension);

        public static bool operator ==(FileLocation? obj1, FileLocation? obj2)
        {
            return Equals(obj1, obj2);
        }

        public static bool operator !=(FileLocation? obj1, FileLocation? obj2)
        {
            return !Equals(obj1, obj2);
        }

        public bool Equals(FileLocation? other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(Directory, other.Directory, StringComparison.OrdinalIgnoreCase) && string.Equals(FileName, other.FileName, StringComparison.OrdinalIgnoreCase) && string.Equals(Extension, other.Extension, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            var a = obj as FileLocation;
            return a != null && Equals(a);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Directory.GetHashCode(StringComparison.OrdinalIgnoreCase);
                hashCode = (hashCode * 397) ^ FileName.GetHashCode(StringComparison.OrdinalIgnoreCase);
                hashCode = (hashCode * 397) ^ Extension.GetHashCode(StringComparison.OrdinalIgnoreCase);
                return hashCode;
            }
        }

        public override string ToString()
        {
            return _filePath ?? (_filePath = Path.Combine(Directory, FileName + Extension));
        }
    }
}
