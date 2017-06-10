using JetBrains.Annotations;
using PhotoReviewer.DAL.Contracts.Data;

namespace PhotoReviewer.DAL.Contracts
{
    public interface ISettingsRepository
    {
        [NotNull]
        ISettings Settings { get; set; }
    }
}