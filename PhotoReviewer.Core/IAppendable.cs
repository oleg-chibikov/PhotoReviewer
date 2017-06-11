namespace PhotoReviewer.Core
{
    public interface IAppendable<T>
    {
        void Append(T t);
    }
}