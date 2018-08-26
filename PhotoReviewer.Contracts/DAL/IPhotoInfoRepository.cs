using System.Collections.Generic;
using JetBrains.Annotations;
using PhotoReviewer.Contracts.DAL.Data;
using Scar.Common.DAL;

namespace PhotoReviewer.Contracts.DAL
{
    // ReSharper disable once UnusedTypeParameter
    public interface IPhotoInfoRepository<TPhotoInfo> : IRepository<TPhotoInfo, FileLocation>
        where TPhotoInfo : IPhotoInfo
    {
        IEnumerable<TPhotoInfo> GetByDirectory([NotNull] string directoryPath);

        void Rename([NotNull] FileLocation oldFileLocation, [NotNull] FileLocation newFileLocation);
    }
}