using PhotoReviewer.Memories.DAL.Data;

namespace PhotoReviewer.Memories.DAL;

public interface IFileRecordRepositoryFactory
{
    void Upsert(IEnumerable<FileRecord> entities);

    void Upsert(FileRecord entity);

    void Delete(FileRecord entity);

    IEnumerable<FileRecord> GetRecords(DateTime startDate, DateTime endDate, int? count);

    YearRange? GetYears();
}