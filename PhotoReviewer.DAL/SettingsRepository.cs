using Common.Logging;
using JetBrains.Annotations;
using PhotoReviewer.DAL.Contracts;
using PhotoReviewer.DAL.Contracts.Model;
using PhotoReviewer.Resources;
using Scar.Common.DAL.LiteDB;

namespace PhotoReviewer.DAL
{
    [UsedImplicitly]
    internal sealed class SettingsRepository : LiteDbRepository<Settings>, ISettingsRepository
    {
        public SettingsRepository([NotNull] ILog logger) : base(logger)
        {
        }

        protected override string DbPath => Paths.SettingsPath;

        public Settings Get()
        {
            return Collection.FindById(1) ?? new Settings();
        }
    }
}