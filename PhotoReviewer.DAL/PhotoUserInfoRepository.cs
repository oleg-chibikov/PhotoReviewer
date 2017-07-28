using System;
using System.Linq;
using Common.Logging;
using JetBrains.Annotations;
using PhotoReviewer.Contracts.DAL;
using PhotoReviewer.Contracts.DAL.Data;
using PhotoReviewer.DAL.Model;

namespace PhotoReviewer.DAL
{
    [UsedImplicitly]
    internal sealed class PhotoUserInfoRepository : IPhotoUserInfoRepository
    {
        [NotNull]
        private readonly IPhotoInfoRepository<FavoritedPhoto> _favoritedPhotoRepository;

        [NotNull]
        private readonly ILog _logger;

        [NotNull]
        private readonly IPhotoInfoRepository<MarkedForDeletionPhoto> _markedForDeletionPhotoRepository;

        public PhotoUserInfoRepository(
            [NotNull] ILog logger,
            [NotNull] IPhotoInfoRepository<MarkedForDeletionPhoto> markedForDeletionPhotoRepository,
            [NotNull] IPhotoInfoRepository<FavoritedPhoto> favoritedPhotoRepository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _markedForDeletionPhotoRepository = markedForDeletionPhotoRepository ?? throw new ArgumentNullException(nameof(markedForDeletionPhotoRepository));
            _favoritedPhotoRepository = favoritedPhotoRepository ?? throw new ArgumentNullException(nameof(favoritedPhotoRepository));
        }

        public PhotoUserInfo Check(string filePath)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            var favorited = _favoritedPhotoRepository.Check(filePath);
            var markedForDeletion = _markedForDeletionPhotoRepository.Check(filePath);
            return new PhotoUserInfo(favorited, markedForDeletion);
        }

        public void Rename(string oldFilePath, string newFilePath)
        {
            _logger.Debug($"Renaming {oldFilePath} to {newFilePath}...");
            if (oldFilePath == null)
                throw new ArgumentNullException(nameof(oldFilePath));
            if (newFilePath == null)
                throw new ArgumentNullException(nameof(newFilePath));

            _favoritedPhotoRepository.Rename(oldFilePath, newFilePath);
            _markedForDeletionPhotoRepository.Rename(oldFilePath, newFilePath);
        }

        public void Delete(string filePath)
        {
            _logger.Trace($"Checking {filePath}...");
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            _favoritedPhotoRepository.Delete(filePath);
            _markedForDeletionPhotoRepository.Delete(filePath);
        }

        public void Favorite(string filePath)
        {
            _logger.Debug($"Favoriting {filePath}...");
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            _favoritedPhotoRepository.Save(
                new FavoritedPhoto
                {
                    Id = filePath
                });
            _markedForDeletionPhotoRepository.Delete(filePath);
        }

        public void Favorite(string[] filePaths)
        {
            _logger.Debug($"Favoriting {filePaths.Length} photos...");
            if (filePaths == null)
                throw new ArgumentNullException(nameof(filePaths));

            _favoritedPhotoRepository.Save(
                filePaths.Select(
                    filePath => new FavoritedPhoto
                    {
                        Id = filePath
                    }));
            _markedForDeletionPhotoRepository.Delete(filePaths);
        }

        public void MarkForDeletion(string filePath)
        {
            _logger.Debug($"Marking {filePath} for deletion...");
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            _markedForDeletionPhotoRepository.Save(
                new MarkedForDeletionPhoto
                {
                    Id = filePath
                });
            _favoritedPhotoRepository.Delete(filePath);
        }

        public void MarkForDeletion(string[] filePaths)
        {
            _logger.Debug($"Marking {filePaths.Length} photos for deletion...");
            if (filePaths == null)
                throw new ArgumentNullException(nameof(filePaths));

            _markedForDeletionPhotoRepository.Save(
                filePaths.Select(
                    filePath => new MarkedForDeletionPhoto
                    {
                        Id = filePath
                    }));
            _favoritedPhotoRepository.Delete(filePaths);
        }

        public void UnFavorite(string filePath)
        {
            _logger.Debug($"Unfavoriting {filePath}...");
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            _favoritedPhotoRepository.Delete(filePath);
        }

        public void UnFavorite(string[] filePaths)
        {
            _logger.Debug($"Unfavoriting {filePaths.Length} photos...");
            if (filePaths == null)
                throw new ArgumentNullException(nameof(filePaths));

            _favoritedPhotoRepository.Delete(filePaths);
        }

        public void UnMarkForDeletion(string filePath)
        {
            _logger.Debug($"Unmarking {filePath} for deletion...");
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            _markedForDeletionPhotoRepository.Delete(filePath);
        }

        public void UnMarkForDeletion(string[] filePaths)
        {
            _logger.Debug($"Unmarking {filePaths.Length} photos for deletion...");
            if (filePaths == null)
                throw new ArgumentNullException(nameof(filePaths));

            _markedForDeletionPhotoRepository.Delete(filePaths);
        }
    }
}