using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using PhotoReviewer.Contracts.DAL;
using PhotoReviewer.Contracts.DAL.Data;
using PhotoReviewer.DAL.Model;

namespace PhotoReviewer.DAL
{
    public sealed class PhotoUserInfoRepository : IPhotoUserInfoRepository
    {
        readonly IPhotoInfoRepository<FavoritedPhoto> _favoritedPhotoRepository;
        readonly ILogger _logger;
        readonly IPhotoInfoRepository<MarkedForDeletionPhoto> _markedForDeletionPhotoRepository;

        public PhotoUserInfoRepository(
            ILogger<PhotoUserInfoRepository> logger,
            IPhotoInfoRepository<MarkedForDeletionPhoto> markedForDeletionPhotoRepository,
            IPhotoInfoRepository<FavoritedPhoto> favoritedPhotoRepository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _markedForDeletionPhotoRepository = markedForDeletionPhotoRepository ?? throw new ArgumentNullException(nameof(markedForDeletionPhotoRepository));
            _favoritedPhotoRepository = favoritedPhotoRepository ?? throw new ArgumentNullException(nameof(favoritedPhotoRepository));
        }

        public PhotoUserInfo Check(FileLocation fileLocation)
        {
            _ = fileLocation ?? throw new ArgumentNullException(nameof(fileLocation));

            _logger.LogTrace($"Checking {fileLocation}...");

            var favorited = _favoritedPhotoRepository.Check(fileLocation);
            var markedForDeletion = _markedForDeletionPhotoRepository.Check(fileLocation);
            return new PhotoUserInfo(favorited, markedForDeletion);
        }

        public IDictionary<FileLocation, PhotoUserInfo> GetUltimateInfo(string directoryPath)
        {
            _ = directoryPath ?? throw new ArgumentNullException(nameof(directoryPath));

            _logger.LogTrace($"Getting ultimate info about all photos in {directoryPath}...");

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
            _ = newFileLocation ?? throw new ArgumentNullException(nameof(newFileLocation));
            _ = oldFileLocation ?? throw new ArgumentNullException(nameof(oldFileLocation));

            _logger.LogDebug($"Renaming {oldFileLocation} to {newFileLocation}...");

            _favoritedPhotoRepository.Rename(oldFileLocation, newFileLocation);
            _markedForDeletionPhotoRepository.Rename(oldFileLocation, newFileLocation);
        }

        public void Delete(FileLocation fileLocation)
        {
            _ = fileLocation ?? throw new ArgumentNullException(nameof(fileLocation));

            _logger.LogDebug($"Deleting {fileLocation}...");

            _favoritedPhotoRepository.Delete(fileLocation);
            _markedForDeletionPhotoRepository.Delete(fileLocation);
        }

        public void Favorite(FileLocation fileLocation)
        {
            _ = fileLocation ?? throw new ArgumentNullException(nameof(fileLocation));

            _logger.LogDebug($"Favoriting {fileLocation}...");

            _favoritedPhotoRepository.Upsert(
                new FavoritedPhoto
                {
                    Id = fileLocation
                });
            _markedForDeletionPhotoRepository.Delete(fileLocation);
        }

        public void Favorite(FileLocation[] fileLocations)
        {
            _ = fileLocations ?? throw new ArgumentNullException(nameof(fileLocations));

            _logger.LogDebug($"Favoriting {fileLocations.Length} photos...");

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
            _ = fileLocation ?? throw new ArgumentNullException(nameof(fileLocation));

            _logger.LogDebug($"Marking {fileLocation} for deletion...");

            _markedForDeletionPhotoRepository.Upsert(
                new MarkedForDeletionPhoto
                {
                    Id = fileLocation
                });
            _favoritedPhotoRepository.Delete(fileLocation);
        }

        public void MarkForDeletion(FileLocation[] fileLocations)
        {
            _ = fileLocations ?? throw new ArgumentNullException(nameof(fileLocations));

            _logger.LogDebug($"Marking {fileLocations.Length} photos for deletion...");

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
            _ = fileLocation ?? throw new ArgumentNullException(nameof(fileLocation));

            _logger.LogDebug($"Unfavoriting {fileLocation}...");

            _favoritedPhotoRepository.Delete(fileLocation);
        }

        public void UnFavorite(FileLocation[] fileLocations)
        {
            _ = fileLocations ?? throw new ArgumentNullException(nameof(fileLocations));

            _logger.LogDebug($"Unfavoriting {fileLocations.Length} photos...");

            _favoritedPhotoRepository.Delete(fileLocations);
        }

        public void UnMarkForDeletion(FileLocation fileLocation)
        {
            _ = fileLocation ?? throw new ArgumentNullException(nameof(fileLocation));

            _logger.LogDebug($"Unmarking {fileLocation} for deletion...");

            _markedForDeletionPhotoRepository.Delete(fileLocation);
        }

        public void UnMarkForDeletion(FileLocation[] fileLocations)
        {
            _ = fileLocations ?? throw new ArgumentNullException(nameof(fileLocations));

            _logger.LogDebug($"Unmarking {fileLocations.Length} photos for deletion...");

            _markedForDeletionPhotoRepository.Delete(fileLocations);
        }
    }
}
