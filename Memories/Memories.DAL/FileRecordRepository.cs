using PhotoReviewer.Memories.DAL.Data;
using Scar.Common.DAL.LiteDB;

namespace PhotoReviewer.Memories.DAL;

public class FileRecordRepository : LiteDbRepository<FileRecord, string>
{
    public FileRecordRepository(string dataFolder, string fileName) : base(
        dataFolder,
        fileName,
        shrink: false,
        requireUpgrade: false,
        isShared: false)
    {
        Collection.EnsureIndex(x => x.DateTaken);
        Collection.EnsureIndex(x => x.Id);
    }
}