using JetBrains.Annotations;
using PhotoReviewer.DAL.Contracts.Data;
using Scar.Common.DAL;

namespace PhotoReviewer.DAL.Contracts
{
    // ReSharper disable once UnusedTypeParameter
    public interface IPhotoInfoRepository<TPhotoInfo> : IRepository<TPhotoInfo, string>
        where TPhotoInfo : IPhotoInfo
    {
        void Rename([NotNull] string oldFilePath, [NotNull] string newFilePath);
    }
}