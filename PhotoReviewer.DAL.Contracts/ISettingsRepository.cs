using JetBrains.Annotations;
using PhotoReviewer.DAL.Contracts.Data;

namespace PhotoReviewer.DAL.Contracts
{
    public interface ISettingsRepository
    {
        [NotNull]
        ISettings Get();

        void Save([NotNull] ISettings settings);
    }
}