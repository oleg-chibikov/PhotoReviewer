using JetBrains.Annotations;

namespace PhotoReviewer.DAL.Contracts.Model
{
    public class MarkedForDeletionPhoto : PhotoInfo
    {
        public MarkedForDeletionPhoto()
        {
        }

        public MarkedForDeletionPhoto([NotNull] string path) : base(path)
        {
        }
    }
}