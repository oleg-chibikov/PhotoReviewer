using JetBrains.Annotations;
using PhotoReviewer.DAL.Contracts.Model;

namespace PhotoReviewer.DAL.Contracts
{
    // ReSharper disable once UnusedTypeParameter
    public interface IPhotoInfoRepository<TPhotoInfo>
        where TPhotoInfo : PhotoInfo
    {
        bool Check([NotNull] string path);
        void Delete([NotNull] string path);
        void Delete([NotNull] string[] paths);
        void Rename([NotNull] string oldPath, [NotNull] string newPath);
        void Save([NotNull] string[] paths);
    }
}