namespace PhotoReviewer.Contracts.DAL.Data
{
    public sealed class PhotoUserInfo
    {
        public PhotoUserInfo(bool favorited, bool markedForDeletion)
        {
            Favorited = favorited;
            MarkedForDeletion = markedForDeletion;
        }

        public bool Favorited { get; }

        public bool MarkedForDeletion { get; }
    }
}