using PhotoReviewer.Contracts.DAL.Data;

namespace PhotoReviewer.Contracts.DAL
{
    public interface ISettingsRepository
    {
        ISettings Settings { get; set; }
    }
}
