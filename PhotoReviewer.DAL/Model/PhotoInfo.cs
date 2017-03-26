using JetBrains.Annotations;
using PhotoReviewer.DAL.Contracts.Data;
using Scar.Common.DAL.Model;

namespace PhotoReviewer.DAL.Model
{
    internal abstract class PhotoInfo : Entity, IPhotoInfo
    {
        //TODO: Use FilePath as Primary Key and use methods from base repository
        [UsedImplicitly]
        // ReSharper disable once NotNullMemberIsNotInitialized
        protected PhotoInfo()
        {
        }

        protected PhotoInfo([NotNull] string filePath)
        {
            FilePath = filePath;
        }

        public string FilePath { get; set; }
    }
}