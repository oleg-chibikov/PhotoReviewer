using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Common.Logging;
using JetBrains.Annotations;
using PhotoReviewer.Contracts.DAL;
using PhotoReviewer.Contracts.DAL.Data;
using PhotoReviewer.Resources;
using Scar.Common.DAL.LiteDB;

namespace PhotoReviewer.DAL
{
    [UsedImplicitly]
    internal sealed class PhotoInfoRepository<TPhotoInfo> : LiteDbRepository<TPhotoInfo, FileLocation>, IPhotoInfoRepository<TPhotoInfo>
        where TPhotoInfo : IPhotoInfo, new()
    {
        public PhotoInfoRepository([NotNull] ILog logger)
            : base(logger)
        {
            Collection.EnsureIndex(x => x.Id.Directory);
            Task.Run(() => CleanNonExisting());
        }

        [NotNull]
        protected override string DbPath => Paths.SettingsPath;

        public IEnumerable<TPhotoInfo> GetByDirectory(string directoryPath)
        {
            Logger.Trace($"Getting all files by directory {directoryPath}");
            if (directoryPath == null)
                throw new ArgumentNullException(nameof(directoryPath));

            return Collection.Find(x => x.Id.Directory.Equals(directoryPath, StringComparison.InvariantCultureIgnoreCase));
        }

        public void Rename(FileLocation oldFileLocation, FileLocation newFileLocation)
        {
            Logger.Debug($"Renaming {oldFileLocation} to {newFileLocation} in the database...");
            if (!Check(oldFileLocation))
            {
                Logger.Warn($"There is no {oldFileLocation} in the database...");
                return;
            }

            Delete(oldFileLocation);
            Save(
                new TPhotoInfo
                {
                    Id = newFileLocation
                });
        }

        private void CleanNonExisting()
        {
            var notExisting = GetAll().Where(photoInfo => !File.Exists(photoInfo.Id.ToString())).ToArray();
            if (!notExisting.Any())
            {
                Logger.Trace("All records are up to date");
                return;
            }

            Logger.Debug($"Deleting {notExisting.Length} non existing photos from the database...");
            Delete(notExisting);
        }
    }
}