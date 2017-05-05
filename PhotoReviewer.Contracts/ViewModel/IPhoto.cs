using JetBrains.Annotations;

namespace PhotoReviewer.Contracts.ViewModel
{
    public interface IPhoto
    {
        [NotNull]
        string FilePath { get; }

        void ReloadCollectionInfoIfNeeded();
    }
}
