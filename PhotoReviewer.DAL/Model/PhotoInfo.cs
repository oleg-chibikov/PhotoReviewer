using PhotoReviewer.DAL.Contracts.Data;
using Scar.Common.DAL.Model;

namespace PhotoReviewer.DAL.Model
{
    internal abstract class PhotoInfo : Entity<string>, IPhotoInfo
    {
    }
}