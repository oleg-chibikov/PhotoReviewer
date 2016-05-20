using JetBrains.Annotations;
using LiteDB;

namespace PhotoReviewer
{
    internal static class DbProvider
    {
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

        public static void Save([NotNull] string path, OperationType operationType)
        {
            using (var db = new LiteDatabase(DbName))
            {
                var dbPhotos = db.GetCollection<DbPhoto>(operationType == OperationType.MarkForDeletion ? MarkedForDeletionTable : FavoritedTable);
                dbPhotos.Insert(new DbPhoto(path));
                dbPhotos.EnsureIndex(x => x.Path);
            }
        }

        public static bool Check([NotNull] string path, OperationType operationType)
        {
            using (var db = new LiteDatabase(DbName))
            {
                var dbPhotos = db.GetCollection<DbPhoto>(operationType == OperationType.MarkForDeletion ? MarkedForDeletionTable : FavoritedTable);
                return dbPhotos.Exists(x => x.Path == path);
            }
        }

        public static void Delete([NotNull] string path, OperationType operationType)
        {
            using (var db = new LiteDatabase(DbName))
            {
                Delete(db, path, operationType);
            }
        }

        public static void Delete([NotNull] string path)
        {
            using (var db = new LiteDatabase(DbName))
            {
                Delete(db, path, OperationType.MarkForDeletion);
                Delete(db, path, OperationType.Favorite);
            }
        }

        private static void Delete([NotNull] LiteDatabase db, string path, OperationType operationType)
        {
            var dbPhotos = db.GetCollection<DbPhoto>(operationType == OperationType.MarkForDeletion ? MarkedForDeletionTable : FavoritedTable);
            dbPhotos.Delete(x => x.Path == path);
        }

        public static void Rename([NotNull] string oldPath, [NotNull] string newPath)
        {
            using (var db = new LiteDatabase(DbName))
            {
                Rename(db, oldPath, newPath, OperationType.MarkForDeletion);
                Rename(db, oldPath, newPath, OperationType.Favorite);
            }
        }

        private static void Rename([NotNull] LiteDatabase db, [NotNull] string oldPath, [NotNull] string newPath, OperationType operationType)
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