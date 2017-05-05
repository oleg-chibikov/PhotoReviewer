using JetBrains.Annotations;
using PhotoReviewer.Contracts.DAL.Data;
using Scar.Common.DAL;

namespace PhotoReviewer.Contracts.DAL
{
    // ReSharper disable once UnusedTypeParameter
    public interface IPhotoInfoRepository<TPhotoInfo> : IRepository<TPhotoInfo, string>
        where TPhotoInfo : IPhotoInfo
    {
        void Rename([NotNull] string oldFilePath, [NotNull] string newFilePath);
    }
}