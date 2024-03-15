using PhotoReviewer.Contracts.DAL.Data;

namespace PhotoReviewer.Contracts.DAL;

public interface IPhotoUserInfoRepository
{
    PhotoUserInfo Check(FileLocation fileLocation);

    void Delete(FileLocation fileLocation);

    void Favorite(FileLocation fileLocation);

    void Favorite(FileLocation[] fileLocations);

    IDictionary<FileLocation, PhotoUserInfo> GetUltimateInfo(string directoryPath);

    void MarkForDeletion(FileLocation fileLocation);

    void MarkForDeletion(FileLocation[] fileLocations);

    void Rename(FileLocation oldFileLocation, FileLocation newFileLocation);

    void UnFavorite(FileLocation fileLocation);

    void UnFavorite(FileLocation[] fileLocations);

    void UnMarkForDeletion(FileLocation fileLocation);

    void UnMarkForDeletion(FileLocation[] fileLocations);
}