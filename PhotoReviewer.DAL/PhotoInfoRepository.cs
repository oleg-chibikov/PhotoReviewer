using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Common.Logging;
using JetBrains.Annotations;
using LiteDB;
using PhotoReviewer.DAL.Contracts;
using PhotoReviewer.DAL.Model;
using PhotoReviewer.Resources;
using Scar.Common.DAL.LiteDB;

namespace PhotoReviewer.DAL
{
    [UsedImplicitly]
    internal abstract class PhotoInfoRepository<TPhotoInfo> : LiteDbRepository<TPhotoInfo>, IPhotoInfoRepository<TPhotoInfo>
        where TPhotoInfo : PhotoInfo, new()
    {
        protected PhotoInfoRepository([NotNull] ILog logger) : base(logger)
        {
            Task.Run(() => {
                CleanNonExisting();
            });
        }

        private void CleanNonExisting()
        {
            var notExisting = GetAll().Select(x => x.FilePath).Where(filePath => !File.Exists(filePath)).ToArray();
            if (!notExisting.Any())
            {
                Logger.Debug($"All records in {typeof(TPhotoInfo)} repository are up to date");
                return;
            }
            Logger.Debug($"Deleting {notExisting.Length} non existing photos from {typeof(TPhotoInfo)} repository...");
            Delete(notExisting);
        }

        protected override string DbPath => Paths.SettingsPath;

        public void Save(string filePath)
        {
            Collection.EnsureIndex(x => x.FilePath);
            Collection.Insert(CreatePhotoInfo(filePath));
        }

        public void Save(string[] filePaths)
        {
            Collection.EnsureIndex(x => x.FilePath);
            foreach (var filePath in filePaths)
                Collection.Insert(CreatePhotoInfo(filePath));
        }

        public bool Check(string filePath)
        {
            return Collection.Exists(x => x.FilePath == filePath);
        }

        public void Delete(string filePath)
        {
            Collection.Delete(x => x.FilePath == filePath);
        }

        public void Delete(string[] filePaths)
        {
            Collection.Delete(Query.In("FilePath", filePaths.Select(x => new BsonValue(x)).ToArray()));
        }

        public void Rename(string oldFilePath, string newFilePath)
        {
            if (!Collection.Exists(x => x.FilePath == oldFilePath))
                return;
            {
                Collection.Delete(x => x.FilePath == oldFilePath);
                Collection.Insert(CreatePhotoInfo(newFilePath));
            }
        }

        [NotNull]
        protected abstract TPhotoInfo CreatePhotoInfo([NotNull] string filePath);
    }
}