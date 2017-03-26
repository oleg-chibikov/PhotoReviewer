using Common.Logging;
using JetBrains.Annotations;
using PhotoReviewer.DAL.Contracts;
using PhotoReviewer.DAL.Contracts.Data;
using PhotoReviewer.DAL.Model;
using PhotoReviewer.Resources;
using Scar.Common.DAL.LiteDB;

namespace PhotoReviewer.DAL
{
    [UsedImplicitly]
    internal sealed class SettingsRepository : LiteDbRepository<Settings, int>, ISettingsRepository
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