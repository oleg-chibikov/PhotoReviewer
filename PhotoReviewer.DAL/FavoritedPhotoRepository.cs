using Common.Logging;
using JetBrains.Annotations;
using PhotoReviewer.DAL.Contracts.Model;

namespace PhotoReviewer.DAL
{
    internal class FavoritedPhotoRepository : PhotoInfoRepository<FavoritedPhoto>
    {
        public FavoritedPhotoRepository([NotNull] ILog logger) : base(logger)
        {
        }

        protected override FavoritedPhoto CreatePhotoInfo(string path)
        {
            return new FavoritedPhoto(path);
        }
    }
}