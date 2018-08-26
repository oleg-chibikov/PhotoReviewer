using System;
using System.Collections.Generic;
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

        public PhotoUserInfo Check(FileLocation fileLocation)
        {
            _logger.Trace($"Checking {fileLocation}...");
            if (fileLocation == null)
            {
                throw new ArgumentNullException(nameof(fileLocation));
            }

            var favorited = _favoritedPhotoRepository.Check(fileLocation);
            var markedForDeletion = _markedForDeletionPhotoRepository.Check(fileLocation);
            return new PhotoUserInfo(favorited, markedForDeletion);
        }

        public IDictionary<FileLocation, PhotoUserInfo> GetUltimateInfo(string directoryPath)
        {
            _logger.Trace($"Getting ultimate info about all photos in {directoryPath}...");
            if (directoryPath == null)
            {
                throw new ArgumentNullException(nameof(directoryPath));
            }

            var allFavorited = _favoritedPhotoRepository.GetByDirectory(directoryPath).Select(favoritedPhoto => favoritedPhoto.Id).ToArray();
            var allMarkedForDeletion = _markedForDeletionPhotoRepository.GetByDirectory(directoryPath).Select(markedForDeletionPhoto => markedForDeletionPhoto.Id).ToArray();
            var intersection = allFavorited.Intersect(allMarkedForDeletion).ToArray();
            var onlyFavorited = allFavorited.Except(intersection);
            var onlyMarked = allMarkedForDeletion.Except(intersection);
            var ultimateInfo = intersection.ToDictionary(fileLocation => fileLocation, photoInfo => new PhotoUserInfo(true, true));
            foreach (var fileLocation in onlyFavorited)
            {
                ultimateInfo.Add(fileLocation, new PhotoUserInfo(true, false));
            }

            foreach (var fileLocation in onlyMarked)
            {
                ultimateInfo.Add(fileLocation, new PhotoUserInfo(false, true));
            }

            return ultimateInfo;
        }

        public void Rename(FileLocation oldFileLocation, FileLocation newFileLocation)
        {
            _logger.Debug($"Renaming {oldFileLocation} to {newFileLocation}...");
            if (oldFileLocation == null)
            {
                throw new ArgumentNullException(nameof(oldFileLocation));
            }

            if (newFileLocation == null)
            {
                throw new ArgumentNullException(nameof(newFileLocation));
            }

            _favoritedPhotoRepository.Rename(oldFileLocation, newFileLocation);
            _markedForDeletionPhotoRepository.Rename(oldFileLocation, newFileLocation);
        }

        public void Delete(FileLocation fileLocation)
        {
            _logger.Debug($"Deleting {fileLocation}...");
            if (fileLocation == null)
            {
                throw new ArgumentNullException(nameof(fileLocation));
            }

            _favoritedPhotoRepository.Delete(fileLocation);
            _markedForDeletionPhotoRepository.Delete(fileLocation);
        }

        public void Favorite(FileLocation fileLocation)
        {
            _logger.Debug($"Favoriting {fileLocation}...");
            if (fileLocation == null)
            {
                throw new ArgumentNullException(nameof(fileLocation));
            }

            _favoritedPhotoRepository.Upsert(
                new FavoritedPhoto
                {
                    Id = fileLocation
                });
            _markedForDeletionPhotoRepository.Delete(fileLocation);
        }

        public void Favorite(FileLocation[] fileLocations)
        {
            _logger.Debug($"Favoriting {fileLocations.Length} photos...");
            if (fileLocations == null)
            {
                throw new ArgumentNullException(nameof(fileLocations));
            }

            _favoritedPhotoRepository.Upsert(
                fileLocations.Select(
                    fileLocation => new FavoritedPhoto
                    {
                        Id = fileLocation
                    }));
            _markedForDeletionPhotoRepository.Delete(fileLocations);
        }

        public void MarkForDeletion(FileLocation fileLocation)
        {
            _logger.Debug($"Marking {fileLocation} for deletion...");
            if (fileLocation == null)
            {
                throw new ArgumentNullException(nameof(fileLocation));
            }

            _markedForDeletionPhotoRepository.Upsert(
                new MarkedForDeletionPhoto
                {
                    Id = fileLocation
                });
            _favoritedPhotoRepository.Delete(fileLocation);
        }

        public void MarkForDeletion(FileLocation[] fileLocations)
        {
            _logger.Debug($"Marking {fileLocations.Length} photos for deletion...");
            if (fileLocations == null)
            {
                throw new ArgumentNullException(nameof(fileLocations));
            }

            _markedForDeletionPhotoRepository.Upsert(
                fileLocations.Select(
                    fileLocation => new MarkedForDeletionPhoto
                    {
                        Id = fileLocation
                    }));
            _favoritedPhotoRepository.Delete(fileLocations);
        }

        public void UnFavorite(FileLocation fileLocation)
        {
            _logger.Debug($"Unfavoriting {fileLocation}...");
            if (fileLocation == null)
            {
                throw new ArgumentNullException(nameof(fileLocation));
            }

            _favoritedPhotoRepository.Delete(fileLocation);
        }

        public void UnFavorite(FileLocation[] fileLocations)
        {
            _logger.Debug($"Unfavoriting {fileLocations.Length} photos...");
            if (fileLocations == null)
            {
                throw new ArgumentNullException(nameof(fileLocations));
            }

            _favoritedPhotoRepository.Delete(fileLocations);
        }

        public void UnMarkForDeletion(FileLocation fileLocation)
        {
            _logger.Debug($"Unmarking {fileLocation} for deletion...");
            if (fileLocation == null)
            {
                throw new ArgumentNullException(nameof(fileLocation));
            }

            _markedForDeletionPhotoRepository.Delete(fileLocation);
        }

        public void UnMarkForDeletion(FileLocation[] fileLocations)
        {
            _logger.Debug($"Unmarking {fileLocations.Length} photos for deletion...");
            if (fileLocations == null)
            {
                throw new ArgumentNullException(nameof(fileLocations));
            }

            _markedForDeletionPhotoRepository.Delete(fileLocations);
        }
    }
}