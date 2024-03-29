namespace PhotoReviewer.Contracts.DAL.Data;

public interface ISettings
{
    string? LastUsedDirectoryPath { get; set; }

    double? LastScrollOffset { get; set; }
}