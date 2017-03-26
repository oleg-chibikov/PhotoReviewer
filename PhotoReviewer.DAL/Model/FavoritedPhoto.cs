using JetBrains.Annotations;

namespace PhotoReviewer.DAL.Model
{
    internal class FavoritedPhoto : PhotoInfo
    {
        public FavoritedPhoto()
        {
        }

        public FavoritedPhoto([NotNull] string filePath) : base(filePath)
        {
        }
    }
}