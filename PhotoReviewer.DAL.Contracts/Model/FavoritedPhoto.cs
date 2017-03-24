using JetBrains.Annotations;

namespace PhotoReviewer.DAL.Contracts.Model
{
    public class FavoritedPhoto : PhotoInfo
    {
        public FavoritedPhoto()
        {
        }

        public FavoritedPhoto([NotNull] string filePath) : base(filePath)
        {
        }
    }
}