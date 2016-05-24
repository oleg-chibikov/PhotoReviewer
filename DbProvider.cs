using System;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using LiteDB;

namespace PhotoReviewer
{
    public class DbProvider : IDisposable
    {
        public enum OperationType
        {
            MarkForDeletion,
            Favorite
        }

        [NotNull]
        private const string DbName = "Data.db";

        [NotNull]
        private const string MarkedForDeletionTable = "markedForDeletion";

        [NotNull]
        private const string FavoritedTable = "favorited";

        private readonly LiteDatabase db;

        public DbProvider()
        {
            var dbFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), nameof(PhotoReviewer));
            if (!Directory.Exists(dbFolder))
                Directory.CreateDirectory(dbFolder);
            db = new LiteDatabase(Path.Combine(dbFolder, DbName));
        }

        public void Dispose()
        {
            db.Dispose();
        }

        public void Save([NotNull] string[] paths, OperationType operationType)
        {
            var dbPhotos = db.GetCollection<DbPhoto>(operationType == OperationType.MarkForDeletion ? MarkedForDeletionTable : FavoritedTable);
            dbPhotos.EnsureIndex(x => x.Path);
            foreach (var path in paths)
                dbPhotos.Insert(new DbPhoto(path));
        }

        public bool Check([NotNull] string path, OperationType operationType)
        {
            var dbPhotos = db.GetCollection<DbPhoto>(operationType == OperationType.MarkForDeletion ? MarkedForDeletionTable : FavoritedTable);
            return dbPhotos.Exists(x => x.Path == path);
        }

        public void Delete([NotNull] string path)
        {
            Delete(path, OperationType.MarkForDeletion);
            Delete(path, OperationType.Favorite);
        }

        private void Delete([NotNull] string path, OperationType operationType)
        {
            var dbPhotos = db.GetCollection<DbPhoto>(operationType == OperationType.MarkForDeletion ? MarkedForDeletionTable : FavoritedTable);
            dbPhotos.Delete(x => x.Path == path);
        }

        public void Delete([NotNull] string[] paths)
        {
            Delete(paths, OperationType.MarkForDeletion);
            Delete(paths, OperationType.Favorite);
        }

        public void Delete([NotNull] string[] paths, OperationType operationType)
        {
            var dbPhotos = db.GetCollection<DbPhoto>(operationType == OperationType.MarkForDeletion ? MarkedForDeletionTable : FavoritedTable);
            dbPhotos.Delete(Query.In("Path", paths.Select(x => new BsonValue(x)).ToArray()));
        }

        public void Rename([NotNull] string oldPath, [NotNull] string newPath)
        {
            Rename(oldPath, newPath, OperationType.MarkForDeletion);
            Rename(oldPath, newPath, OperationType.Favorite);
        }

        private void Rename([NotNull] string oldPath, [NotNull] string newPath, OperationType operationType)
        {
            var dbPhotos = db.GetCollection<DbPhoto>(operationType == OperationType.MarkForDeletion ? MarkedForDeletionTable : FavoritedTable);
            if (!dbPhotos.Exists(x => x.Path == oldPath))
                return;
            {
                dbPhotos.Delete(x => x.Path == oldPath);
                dbPhotos.Insert(new DbPhoto(newPath));
            }
        }

        private class DbPhoto
        {
            // ReSharper disable once NotNullMemberIsNotInitialized
            public DbPhoto()
            {
            }

            public DbPhoto(string path)
            {
                Path = path;
            }

            [NotNull]
            public string Path
            {
                get;
                // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
                [UsedImplicitly]
                set;
            }
        }
    }
}