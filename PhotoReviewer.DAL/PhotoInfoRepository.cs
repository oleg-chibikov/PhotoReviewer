using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Common.Logging;
using JetBrains.Annotations;
using PhotoReviewer.DAL.Contracts;
using PhotoReviewer.DAL.Contracts.Data;
using PhotoReviewer.Resources;
using Scar.Common.DAL.LiteDB;

namespace PhotoReviewer.DAL
{
    [UsedImplicitly]
    internal class PhotoInfoRepository<TPhotoInfo> : LiteDbRepository<TPhotoInfo, string>, IPhotoInfoRepository<TPhotoInfo>
        where TPhotoInfo : IPhotoInfo, new()
    {
        public PhotoInfoRepository([NotNull] ILog logger) : base(logger)
        {
            Task.Run(() => { CleanNonExisting(); });
        }

        protected override string DbPath => Paths.SettingsPath;

        public void Rename(string oldFilePath, string newFilePath)
        {
            Logger.Debug($"Renaming {oldFilePath} to {newFilePath} in database...");
            if (!Check(oldFilePath))
                return;
            {
                Collection.Delete(oldFilePath);
                Collection.Insert(new TPhotoInfo { Id = newFilePath });
            }
        }

        private void CleanNonExisting()
        {
            var notExisting = GetAll().Where(photoInfo => !File.Exists(photoInfo.Id)).ToArray();
            if (!notExisting.Any())
            {
                Logger.Debug("All records are up to date");
                return;
            }
            Logger.Debug($"Deleting {notExisting.Length} non existing photos from database...");
            Delete(notExisting);
        }
    }
}