namespace PhotoReviewer.Memories.DAL.Data;

public interface IRepositorySettings
{
    public string DataFolder { get; }

    public string FileRecordsFolder { get; }
}