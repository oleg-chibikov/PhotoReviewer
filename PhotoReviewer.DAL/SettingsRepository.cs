using JetBrains.Annotations;
using PhotoReviewer.Contracts.DAL;
using PhotoReviewer.Contracts.DAL.Data;
using PhotoReviewer.DAL.Model;
using Scar.Common.DAL.LiteDB;
using Scar.Common.IO;

namespace PhotoReviewer.DAL
{
    [UsedImplicitly]
    internal sealed class SettingsRepository : LiteDbRepository<Settings, int>, ISettingsRepository
    {
        public SettingsRepository()
            : base(CommonPaths.SettingsPath)
        {
        }

        public ISettings Settings
        {
            get => Collection.FindById(1) ?? new Settings();
            set => Upsert((Settings)value);
        }
    }
}