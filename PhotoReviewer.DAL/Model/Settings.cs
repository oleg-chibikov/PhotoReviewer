using JetBrains.Annotations;
using PhotoReviewer.Contracts.DAL.Data;
using Scar.Common.DAL.Model;

namespace PhotoReviewer.DAL.Model
{
    internal sealed class Settings : Entity<int>, ISettings
    {
        [CanBeNull]
        public string LastUsedDirectoryPath { get; set; }
    }
}