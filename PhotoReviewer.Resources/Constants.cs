using System.Linq;

namespace PhotoReviewer.Resources
{
    public static class Constants
    {
        public static readonly string[] FileExtensions = {".png", ".jpg", ".jpeg", ".bmp"};
        public static readonly string[] FilterExtensions = FileExtensions.Select(x => $"*{x}").ToArray();
    }
}