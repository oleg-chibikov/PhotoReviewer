using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Common.Logging;
using JetBrains.Annotations;
using PhotoReviewer.DAL.Contracts;
using PhotoReviewer.DAL.Contracts.Model;

namespace PhotoReviewer.Core
{
    [UsedImplicitly]
    internal class NonExistingItemsCleaner
    {
        [NotNull]
        private readonly ILog logger;

        public NonExistingItemsCleaner([NotNull] IPhotoInfoRepository<FavoritedPhoto> favoritedPhotoRepository, [NotNull] IPhotoInfoRepository<MarkedForDeletionPhoto> markedForDeletionPhotoRepository, [NotNull] ILog logger)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            this.logger = logger;
            Task.Run(() =>
            {
                //TODO: make repository readonly for this operation (write lock)
                CleanNonExisting(markedForDeletionPhotoRepository);

                //TODO: make repository readonly for this operation (write lock)
                CleanNonExisting(favoritedPhotoRepository);
            });
        }

        private void CleanNonExisting<TPhotoInfo>(IPhotoInfoRepository<TPhotoInfo> repository)
            where TPhotoInfo : PhotoInfo
        {
            var notExisting = repository.GetAll().Select(x => x.FilePath).Where(filePath => !File.Exists(filePath)).ToArray();
            if (!notExisting.Any())
            {
                logger.Debug($"All records in {typeof(TPhotoInfo)} repository are up to date");
                return;
            }
            logger.Debug($"Deleting {notExisting.Length} non existing photos from {typeof(TPhotoInfo)} repository...");
            repository.Delete(notExisting);
        }
    }
}