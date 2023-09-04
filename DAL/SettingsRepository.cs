using System;
using PhotoReviewer.Contracts.DAL;
using PhotoReviewer.Contracts.DAL.Data;
using PhotoReviewer.DAL.Model;
using Scar.Common.ApplicationLifetime.Contracts;
using Scar.Common.DAL.LiteDB;

namespace PhotoReviewer.DAL
{
    public sealed class SettingsRepository : LiteDbRepository<Settings, int>, ISettingsRepository
    {
        public SettingsRepository(IAssemblyInfoProvider assemblyInfoProvider)
            : base(assemblyInfoProvider?.SettingsPath ?? throw new ArgumentNullException(nameof(assemblyInfoProvider)))
        {
        }

        public ISettings Settings
        {
            get => Collection.FindById(1) ?? new Settings();
            set => Upsert((Settings)value);
        }
    }
}
