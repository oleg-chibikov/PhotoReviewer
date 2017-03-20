using System.Linq;
using Common.Logging;
using JetBrains.Annotations;
using LiteDB;
using PhotoReviewer.DAL.Contracts;
using PhotoReviewer.DAL.Contracts.Model;
using PhotoReviewer.Resources;
using Scar.Common.DAL.LiteDB;

namespace PhotoReviewer.DAL
{
    [UsedImplicitly]
    public abstract class PhotoInfoRepository<TPhotoInfo> : LiteDbRepository<TPhotoInfo>, IPhotoInfoRepository<TPhotoInfo>
        where TPhotoInfo : PhotoInfo, new()
    {
        protected PhotoInfoRepository([NotNull] ILog logger) : base(logger)
        {
        }

        protected override string DbPath => Paths.SettingsPath;

        public void Save(string[] paths)
        {
            Collection.EnsureIndex(x => x.Path);
            foreach (var path in paths)
                Collection.Insert(CreatePhotoInfo(path));
        }

        public bool Check(string path)
        {
            return Collection.Exists(x => x.Path == path);
        }

        public void Delete(string path)
        {
            Collection.Delete(x => x.Path == path);
        }

        public void Delete(string[] paths)
        {
            Collection.Delete(Query.In("Path", paths.Select(x => new BsonValue(x)).ToArray()));
        }

        public void Rename(string oldPath, string newPath)
        {
            if (!Collection.Exists(x => x.Path == oldPath))
                return;
            {
                Collection.Delete(x => x.Path == oldPath);
                Collection.Insert(CreatePhotoInfo(newPath));
            }
        }

        [NotNull]
        protected abstract TPhotoInfo CreatePhotoInfo([NotNull] string path);
    }
}