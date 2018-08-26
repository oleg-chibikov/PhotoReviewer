using System;
using System.IO;
using JetBrains.Annotations;
// ReSharper disable NonReadonlyMemberInGetHashCode

namespace PhotoReviewer.Contracts.DAL.Data
{
    public sealed class FileLocation : IEquatable<FileLocation>
    {
        [NotNull]
        private const string FavoriteDirectoryName = "Favorite";

        [CanBeNull]
        private string _filePath;

        [UsedImplicitly]
        // ReSharper disable once NotNullMemberIsNotInitialized
        public FileLocation()
        {
        }

        public FileLocation([NotNull] string filePath)
        {
            Directory = Path.GetDirectoryName(filePath) ?? throw new ArgumentNullException(nameof(filePath));
            FileName = Path.GetFileNameWithoutExtension(filePath);
            Extension = Path.GetExtension(filePath);
        }

        [NotNull]
        public string Directory
        {
            get;
            [UsedImplicitly]
            set;
        }

        [NotNull]
        public string FileName
        {
            get;
            [UsedImplicitly]
            set;
        }

        [NotNull]
        public string Extension
        {
            get;
            [UsedImplicitly]
            set;
        }

        [NotNull]
        public string FavoriteDirectory => Path.Combine(Directory, FavoriteDirectoryName);

        [NotNull]
        public string FavoriteFilePath => Path.Combine(FavoriteDirectory, FileName + Extension);

        public bool Equals(FileLocation other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(Directory, other.Directory) && string.Equals(FileName, other.FileName) && string.Equals(Extension, other.Extension);
        }

        public override bool Equals(object obj)
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
                var hashCode = Directory.GetHashCode();
                hashCode = (hashCode * 397) ^ FileName.GetHashCode();
                hashCode = (hashCode * 397) ^ Extension.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==([CanBeNull] FileLocation obj1, [CanBeNull] FileLocation obj2)
        {
            return Equals(obj1, obj2);
        }

        public static bool operator !=([CanBeNull] FileLocation obj1, [CanBeNull] FileLocation obj2)
        {
            return !Equals(obj1, obj2);
        }

        public override string ToString()
        {
            return _filePath ?? (_filePath = Path.Combine(Directory, FileName + Extension));
        }
    }
}