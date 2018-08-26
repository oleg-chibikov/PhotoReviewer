using JetBrains.Annotations;
using PhotoReviewer.Contracts.DAL.Data;

namespace PhotoReviewer.Contracts.DAL
{
    public interface ISettingsRepository
    {
        [NotNull]
        ISettings Settings { get; set; }
    }
}