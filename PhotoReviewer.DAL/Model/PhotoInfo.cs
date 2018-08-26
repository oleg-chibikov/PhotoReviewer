using PhotoReviewer.Contracts.DAL.Data;
using Scar.Common.DAL.Model;

namespace PhotoReviewer.DAL.Model
{
    internal abstract class PhotoInfo : Entity<FileLocation>, IPhotoInfo
    {
    }
}