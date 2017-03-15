using System;
using JetBrains.Annotations;

namespace PhotoReviewer.ViewModel
{
    public class PhotoDeletedEventArgs
    {
        public PhotoDeletedEventArgs([NotNull] string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            Path = path;
        }

        [NotNull]
        public string Path { get; private set; }
    }
}