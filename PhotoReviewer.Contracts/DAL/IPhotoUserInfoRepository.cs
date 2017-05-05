using JetBrains.Annotations;
using PhotoReviewer.Contracts.DAL.Data;

namespace PhotoReviewer.Contracts.DAL
{
    public interface IPhotoUserInfoRepository
    {
        [NotNull]
        PhotoUserInfo Check([NotNull] string filePath);

        void Rename([NotNull] string oldFilePath, [NotNull] string newFilePath);
        void Delete([NotNull] string filePath);
        void Favorite([NotNull] string filePath);
        void MarkForDeletion([NotNull] string filePath);
        void UnFavorite([NotNull] string filePath);
        void UnMarkForDeletion([NotNull] string filePath);
        void MarkForDeletion([NotNull] string[] filePaths);
        void Favorite([NotNull] string[] filePaths);
        void UnMarkForDeletion([NotNull] string[] filePaths);
        void UnFavorite([NotNull] string[] filePaths);
    }
}