using JetBrains.Annotations;
using LiteDB;

namespace PhotoReviewer
{
    internal static class DbProvider
    {
        private class DbPhoto
        {
            public DbPhoto()
            {
            }

            public DbPhoto(string path)
            {
                Path = path;
            }

            public string Path
            {
                get;
                // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
                [UsedImplicitly]
                set;
            }
        }

        const string DbName = "Data.db";
        const string MarkedForDeletionTable = "markedForDeletion";
        const string FavoritedTable = "favorited";

        public enum OperationType
        {
            MarkForDeletion,
            Favorite
        }

        public static void Save(string path, OperationType operationType)
        {
            using (var db = new LiteDatabase(DbName))
            {
                var dbPhotos = db.GetCollection<DbPhoto>(operationType==OperationType.MarkForDeletion?MarkedForDeletionTable: FavoritedTable);
                var dbPhoto = new DbPhoto(path);
                dbPhotos.Insert(dbPhoto);
                dbPhotos.EnsureIndex(x => x.Path);
            }
        }

        public static bool Check(string path, OperationType operationType)
        {
            using (var db = new LiteDatabase(DbName))
            {
                var dbPhotos = db.GetCollection<DbPhoto>(operationType == OperationType.MarkForDeletion ? MarkedForDeletionTable : FavoritedTable);
                return dbPhotos.Exists(x => x.Path == path);
            }
        }

        public static void Delete(string path, OperationType operationType)
        {
            using (var db = new LiteDatabase(DbName))
            {
                var dbPhotos = db.GetCollection<DbPhoto>(operationType == OperationType.MarkForDeletion ? MarkedForDeletionTable : FavoritedTable);
                dbPhotos.Delete(x => x.Path == path);
            }
        }
    }
}