using System;
using System.Linq;
using JetBrains.Annotations;
using LiteDB;

namespace PhotoReviewer
{
    public class DbProvider : IDisposable
    {
        private readonly LiteDatabase db;

        public DbProvider()
        {
            db = new LiteDatabase(DbName);
        }

        public void Dispose()
        {
            db.Dispose();
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

        [NotNull]
        const string DbName = "Data.db";

        [NotNull]
        const string MarkedForDeletionTable = "markedForDeletion";

        [NotNull]
        const string FavoritedTable = "favorited";

        public enum OperationType
        {
            MarkForDeletion,
            Favorite
        }

        public void Save([NotNull] string path, OperationType operationType)
        {
            var dbPhotos = db.GetCollection<DbPhoto>(operationType == OperationType.MarkForDeletion ? MarkedForDeletionTable : FavoritedTable);
            dbPhotos.Insert(new DbPhoto(path));
            dbPhotos.EnsureIndex(x => x.Path);
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

        public void Delete([NotNull] string path, OperationType operationType)
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
    }
}