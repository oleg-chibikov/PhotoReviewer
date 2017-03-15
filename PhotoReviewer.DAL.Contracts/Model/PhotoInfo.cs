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

        public PhotoInfo([NotNull] string path)
        {
            Path = path;
        }

        [NotNull]
        public string Path { get; set; }
    }
}