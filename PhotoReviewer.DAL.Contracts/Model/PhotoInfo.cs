using JetBrains.Annotations;
using Scar.Common.DAL.Model;

namespace PhotoReviewer.DAL.Contracts.Model
{
    public abstract class PhotoInfo : Entity
    {
        [UsedImplicitly]
        public PhotoInfo()
        {
        }

        public PhotoInfo([NotNull] string filePath)
        {
            FilePath = filePath;
        }

        [NotNull]
        public string FilePath { get; set; }
    }
}