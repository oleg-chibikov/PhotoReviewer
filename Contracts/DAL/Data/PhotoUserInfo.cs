namespace PhotoReviewer.Contracts.DAL.Data;

public sealed class PhotoUserInfo(bool favorited, bool markedForDeletion)
{
    public bool Favorited { get; } = favorited;

    public bool MarkedForDeletion { get; } = markedForDeletion;
}
