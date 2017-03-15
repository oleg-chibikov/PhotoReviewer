using JetBrains.Annotations;
using PhotoReviewer.DAL.Contracts.Model;
using Scar.Common.DAL;

namespace PhotoReviewer.DAL.Contracts
{
    public interface ISettingsRepository : IRepository<Settings>
    {
        [NotNull]
        Settings Get();
    }
}