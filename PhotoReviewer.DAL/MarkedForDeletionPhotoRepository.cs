using Common.Logging;
using JetBrains.Annotations;
using PhotoReviewer.DAL.Contracts.Model;

namespace PhotoReviewer.DAL
{
    internal class MarkedForDeletionPhotoRepository : PhotoInfoRepository<MarkedForDeletionPhoto>
    {
        public MarkedForDeletionPhotoRepository([NotNull] ILog logger) : base(logger)
        {
        }

        protected override MarkedForDeletionPhoto CreatePhotoInfo(string path)
        {
            return new MarkedForDeletionPhoto(path);
        }
    }
}