using System;
using System.Linq;
using Common.Logging;
using JetBrains.Annotations;
using PhotoReviewer.DAL.Contracts;
using PhotoReviewer.DAL.Contracts.Data;
using PhotoReviewer.DAL.Model;

namespace PhotoReviewer.DAL
{
    [UsedImplicitly]
    internal class PhotoUserInfoRepository : IPhotoUserInfoRepository
    {
        [NotNull]
        private readonly IPhotoInfoRepository<FavoritedPhoto> favoritedPhotoRepository;

        [NotNull]
        private readonly ILog logger;

        [NotNull]
        private readonly IPhotoInfoRepository<MarkedForDeletionPhoto> markedForDeletionPhotoRepository;

        public PhotoUserInfoRepository([NotNull] ILog logger,
            [NotNull] IPhotoInfoRepository<MarkedForDeletionPhoto> markedForDeletionPhotoRepository,
            [NotNull] IPhotoInfoRepository<FavoritedPhoto> favoritedPhotoRepository)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            if (markedForDeletionPhotoRepository == null)
                throw new ArgumentNullException(nameof(markedForDeletionPhotoRepository));
            if (favoritedPhotoRepository == null)
                throw new ArgumentNullException(nameof(favoritedPhotoRepository));

            this.logger = logger;
            this.markedForDeletionPhotoRepository = markedForDeletionPhotoRepository;
            this.favoritedPhotoRepository = favoritedPhotoRepository;
        }

        public PhotoUserInfo Check(string filePath)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));
            var favorited = favoritedPhotoRepository.Check(filePath);
            var markedForDeletion = markedForDeletionPhotoRepository.Check(filePath);
            return new PhotoUserInfo(favorited, markedForDeletion);
        }

        public void Rename(string oldFilePath, string newFilePath)
        {
            logger.Debug($"Renaming {oldFilePath} to {newFilePath}...");
            if (oldFilePath == null)
                throw new ArgumentNullException(nameof(oldFilePath));
            if (newFilePath == null)
                throw new ArgumentNullException(nameof(newFilePath));
            favoritedPhotoRepository.Rename(oldFilePath, newFilePath);
            markedForDeletionPhotoRepository.Rename(oldFilePath, newFilePath);
        }

        public void Delete(string filePath)
        {
            logger.Debug($"Checking {filePath}...");
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));
            favoritedPhotoRepository.Delete(filePath);
            markedForDeletionPhotoRepository.Delete(filePath);
        }

        public void Favorite(string filePath)
        {
            logger.Debug($"Favoriting {filePath}...");
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));
            favoritedPhotoRepository.Save(new FavoritedPhoto { Id = filePath });
            markedForDeletionPhotoRepository.Delete(filePath);
        }

        public void Favorite(string[] filePaths)
        {
            logger.Debug($"Favoriting {filePaths.Length} photos...");
            if (filePaths == null)
                throw new ArgumentNullException(nameof(filePaths));
            favoritedPhotoRepository.Save(filePaths.Select(filePath => new FavoritedPhoto { Id = filePath }));
            markedForDeletionPhotoRepository.Delete(filePaths);
        }

        public void MarkForDeletion(string filePath)
        {
            logger.Debug($"Marking {filePath} for deletion...");
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));
            markedForDeletionPhotoRepository.Save(new MarkedForDeletionPhoto { Id = filePath });
            favoritedPhotoRepository.Delete(filePath);
        }

        public void MarkForDeletion(string[] filePaths)
        {
            logger.Debug($"Marking {filePaths.Length} photos for deletion...");
            if (filePaths == null)
                throw new ArgumentNullException(nameof(filePaths));
            markedForDeletionPhotoRepository.Save(filePaths.Select(filePath => new MarkedForDeletionPhoto { Id = filePath }));
            favoritedPhotoRepository.Delete(filePaths);
        }

        public void UnFavorite(string filePath)
        {
            logger.Debug($"Unfavoriting {filePath}...");
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));
            favoritedPhotoRepository.Delete(filePath);
        }

        public void UnFavorite(string[] filePaths)
        {
            logger.Debug($"Unfavoriting {filePaths.Length} photos...");
            if (filePaths == null)
                throw new ArgumentNullException(nameof(filePaths));
            favoritedPhotoRepository.Delete(filePaths);
        }

        public void UnMarkForDeletion(string filePath)
        {
            logger.Debug($"Unmarking {filePath} for deletion...");
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));
            markedForDeletionPhotoRepository.Delete(filePath);
        }

        public void UnMarkForDeletion(string[] filePaths)
        {
            logger.Debug($"Unmarking {filePaths.Length} photos for deletion...");
            if (filePaths == null)
                throw new ArgumentNullException(nameof(filePaths));
            markedForDeletionPhotoRepository.Delete(filePaths);
        }
    }
}