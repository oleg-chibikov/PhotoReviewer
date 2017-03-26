using Common.Logging;
using JetBrains.Annotations;
using PhotoReviewer.DAL.Model;

namespace PhotoReviewer.DAL
{
    internal class FavoritedPhotoRepository : PhotoInfoRepository<FavoritedPhoto>
    {
        public FavoritedPhotoRepository([NotNull] ILog logger) : base(logger)
        {
        }

        protected override FavoritedPhoto CreatePhotoInfo(string filePath)
        {
            return new FavoritedPhoto(filePath);
        }
    }
}