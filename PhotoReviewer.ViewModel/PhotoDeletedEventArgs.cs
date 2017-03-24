using System;
using JetBrains.Annotations;

namespace PhotoReviewer.ViewModel
{
    public class PhotoDeletedEventArgs
    {
        public PhotoDeletedEventArgs([NotNull] string filePath)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));
            FilePath = filePath;
        }

        [NotNull]
        public string FilePath { get; private set; }
    }
}