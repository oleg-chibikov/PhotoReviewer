using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Common.Logging;
using JetBrains.Annotations;
using PhotoReviewer.Contracts.DAL;
using PhotoReviewer.Contracts.DAL.Data;
using Scar.Common.DAL.LiteDB;
using Scar.Common.IO;

namespace PhotoReviewer.DAL
{
    [UsedImplicitly]
    internal sealed class PhotoInfoRepository<TPhotoInfo> : LiteDbRepository<TPhotoInfo, FileLocation>, IPhotoInfoRepository<TPhotoInfo>
        where TPhotoInfo : IPhotoInfo, new()
    {
        [NotNull]
        private readonly ILog _logger;

        public PhotoInfoRepository([NotNull] ILog logger)
            : base(CommonPaths.SettingsPath)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            //TODO: This index cannot handle keys more than 512 bytes. So that filepath length is limited. Maybe store hash of filepath to find photos
            Collection.EnsureIndex(x => x.Id.Directory);
            Task.Run(() => CleanNonExisting());
        }

        public IEnumerable<TPhotoInfo> GetByDirectory(string directoryPath)
        {
            _logger.Trace($"Getting all files by directory {directoryPath}...");
            if (directoryPath == null)
            {
                throw new ArgumentNullException(nameof(directoryPath));
            }

            return Collection.Find(x => x.Id.Directory.Equals(directoryPath, StringComparison.InvariantCultureIgnoreCase));
        }

        public void Rename(FileLocation oldFileLocation, FileLocation newFileLocation)
        {
            _logger.Debug($"Renaming {oldFileLocation} to {newFileLocation} in the database...");
            if (!Check(oldFileLocation))
            {
                _logger.Warn($"There is no {oldFileLocation} in the database...");
                return;
            }

            Delete(oldFileLocation);
            Upsert(
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
                _logger.Trace("All records are up to date");
                return;
            }

            _logger.Debug($"Deleting {notExisting.Length} non existing photos from the database...");
            Delete(notExisting);
        }
    }
}