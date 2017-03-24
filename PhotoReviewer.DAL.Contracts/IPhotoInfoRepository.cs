using JetBrains.Annotations;
using PhotoReviewer.DAL.Contracts.Model;
using Scar.Common.DAL;

namespace PhotoReviewer.DAL.Contracts
{
    // ReSharper disable once UnusedTypeParameter
    public interface IPhotoInfoRepository<TPhotoInfo> : IRepository<TPhotoInfo>
        where TPhotoInfo : PhotoInfo
    {
        bool Check([NotNull] string filePath);
        void Delete([NotNull] string filePath);
        void Delete([NotNull] string[] filePaths);
        void Rename([NotNull] string oldFilePath, [NotNull] string newFilePath);
        void Save([NotNull] string filePath);
        void Save([NotNull] string[] filePaths);
    }
}