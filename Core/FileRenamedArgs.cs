namespace PhotoReviewer.Core;

public class FileRenamedArgs
{
    public FileRenamedArgs(string oldPath, string newPath)
    {
        _ = oldPath ?? throw new ArgumentNullException(nameof(oldPath));
        _ = newPath ?? throw new ArgumentNullException(nameof(newPath));
        OldPath = oldPath;
        NewPath = newPath;
    }

    public string OldPath { get; set; }

    public string NewPath { get; set; }
}