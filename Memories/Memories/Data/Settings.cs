using System.IO;
using PhotoReviewer.Memories.DAL.Data;

namespace PhotoReviewer.Memories.Data;

public sealed class Settings(
    string environment,
    TimeSpan jobRunInterval,
    string dataFolder,
    string libraryFolder) : IRepositorySettings
{
    public string Environment { get; } = environment ?? throw new ArgumentNullException(nameof(environment));

    public TimeSpan JobRunInterval { get; } = jobRunInterval;

    public string DataFolder { get; } = dataFolder ?? throw new ArgumentNullException(nameof(dataFolder));

    public string FileRecordsFolder => Path.Combine(
        DataFolder,
        "FileRecords");

    public string LibraryFolder { get; } = libraryFolder ?? throw new ArgumentNullException(nameof(libraryFolder));
}