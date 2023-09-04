using PhotoReviewer.Contracts.DAL.Data;
using Scar.Common.DAL.Contracts.Model;

namespace PhotoReviewer.DAL.Model
{
    public abstract class PhotoInfo : Entity<FileLocation>, IPhotoInfo
    {
    }
}
