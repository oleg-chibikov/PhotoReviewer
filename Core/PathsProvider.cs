namespace PhotoReviewer.Core;

public static class PathsProvider
{
    public static readonly string LogsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        nameof(Scar),
        nameof(PhotoReviewer),
        "Logs",
        "Full.log");
}