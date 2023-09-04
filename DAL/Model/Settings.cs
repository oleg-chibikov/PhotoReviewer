using PhotoReviewer.Contracts.DAL.Data;
using Scar.Common.DAL.Contracts.Model;

namespace PhotoReviewer.DAL.Model
{
    public sealed class Settings : Entity<int>, ISettings
    {
        public string? LastUsedDirectoryPath { get; set; }
    }
}
