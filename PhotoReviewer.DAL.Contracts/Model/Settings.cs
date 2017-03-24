using JetBrains.Annotations;
using Scar.Common.DAL.Model;

namespace PhotoReviewer.DAL.Contracts.Model
{
    public sealed class Settings : Entity
    {
        [CanBeNull]
        public string LastUsedDirectoryPath { get; set; }
    }
}