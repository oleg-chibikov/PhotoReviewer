using System.Collections.Generic;
using JetBrains.Annotations;
using PhotoReviewer.Contracts.DAL.Data;

namespace PhotoReviewer.Contracts.DAL
{
    public interface IPhotoUserInfoRepository
    {
        [NotNull]
        PhotoUserInfo Check([NotNull] FileLocation fileLocation);

        void Delete([NotNull] FileLocation fileLocation);

        void Favorite([NotNull] FileLocation fileLocation);

        void Favorite([NotNull] FileLocation[] fileLocations);

        [NotNull]
        IDictionary<FileLocation, PhotoUserInfo> GetUltimateInfo([NotNull] string directoryPath);

        void MarkForDeletion([NotNull] FileLocation fileLocation);

        void MarkForDeletion([NotNull] FileLocation[] fileLocations);

        void Rename([NotNull] FileLocation oldFileLocation, [NotNull] FileLocation newFileLocation);

        void UnFavorite([NotNull] FileLocation fileLocation);

        void UnFavorite([NotNull] FileLocation[] fileLocations);

        void UnMarkForDeletion([NotNull] FileLocation fileLocation);

        void UnMarkForDeletion([NotNull] FileLocation[] fileLocations);
    }
}