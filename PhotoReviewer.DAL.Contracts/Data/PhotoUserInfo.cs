namespace PhotoReviewer.DAL.Contracts.Data
{
    public class PhotoUserInfo
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
