using Common.Logging;
using JetBrains.Annotations;
using PhotoReviewer.DAL.Contracts;
using PhotoReviewer.DAL.Contracts.Data;
using PhotoReviewer.Resources;
using Scar.Common.DAL.LiteDB;
using Settings = PhotoReviewer.DAL.Model.Settings;

namespace PhotoReviewer.DAL
{
    [UsedImplicitly]
    internal sealed class SettingsRepository : LiteDbRepository<Settings>, ISettingsRepository
    {
        public SettingsRepository([NotNull] ILog logger) : base(logger)
        {
        }

        protected override string DbPath => Paths.SettingsPath;

        public ISettings Get()
        {
            return Collection.FindById(1) ?? new Settings();
        }

        public void Save(ISettings settings)
        {
            base.Save((Settings)settings);
        }
    }
}