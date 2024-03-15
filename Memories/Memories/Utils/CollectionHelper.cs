namespace PhotoReviewer.Memories.Utils;

static class CollectionHelper
{
    public static IEnumerable<T> GetRandomItems<T>(this IEnumerable<T> collection, int count)
    {
        var rand = new Random();
        return collection.OrderBy(item => rand.Next()).Take(count);
    }
}