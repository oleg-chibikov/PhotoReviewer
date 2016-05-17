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
        const string TableName = "markedForDeletion";

        public static void Save(string path)
        {
            using (var db = new LiteDatabase(DbName))
            {
                var dbPhotos = db.GetCollection<DbPhoto>(TableName);
                var dbPhoto = new DbPhoto(path);
                dbPhotos.Insert(dbPhoto);
                dbPhotos.EnsureIndex(x => x.Path);
            }
        }

        public static bool IsMarkedForDeletion(string path)
        {
            using (var db = new LiteDatabase(DbName))
            {
                var dbPhotos = db.GetCollection<DbPhoto>(TableName);
                return dbPhotos.Exists(x => x.Path == path);
            }
        }

        public static void Delete(string path)
        {
            using (var db = new LiteDatabase(DbName))
            {
                var dbPhotos = db.GetCollection<DbPhoto>(TableName);
                dbPhotos.Delete(x => x.Path == path);
            }
        }
    }
}