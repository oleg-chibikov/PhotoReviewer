using PhotoReviewer.Contracts.DAL.Data;
using Scar.Common.DAL.Contracts;

namespace PhotoReviewer.Contracts.DAL;

public interface IPhotoInfoRepository<TPhotoInfo> : IRepository<TPhotoInfo, FileLocation>
    where TPhotoInfo : IPhotoInfo
{
    IEnumerable<TPhotoInfo> GetByDirectory(string directoryPath);

    void Rename(FileLocation oldFileLocation, FileLocation newFileLocation);
}