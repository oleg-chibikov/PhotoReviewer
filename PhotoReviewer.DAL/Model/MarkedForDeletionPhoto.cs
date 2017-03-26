using JetBrains.Annotations;

namespace PhotoReviewer.DAL.Model
{
    internal class MarkedForDeletionPhoto : PhotoInfo
    {
        public MarkedForDeletionPhoto()
        {
        }

        public MarkedForDeletionPhoto([NotNull] string filePath) : base(filePath)
        {
        }
    }
}