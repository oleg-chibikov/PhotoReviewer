using System.Globalization;
using PhotoReviewer.Memories.DAL.Data;

namespace PhotoReviewer.Memories.DAL;

public class FileRecordRepositoryFactory(IRepositorySettings settings) : IFileRecordRepositoryFactory
{
    readonly IRepositorySettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));

    public void Upsert(IEnumerable<FileRecord> entities)
    {
        _ = entities ?? throw new ArgumentNullException(nameof(entities));
        var entitiesByDay = entities.GroupBy(e => e.DateTaken.Date); // Assuming entities have DateTaken property
        foreach (var group in entitiesByDay)
        {
            using var repository = CreateRepositoryForDay(group.Key);
            repository.Upsert(group);
        }
    }

    public void Upsert(FileRecord entity)
    {
        _ = entity ?? throw new ArgumentNullException(nameof(entity));
        using var repository = CreateRepositoryForDay(entity.DateTaken.Date);
        repository.Upsert(entity);
    }

    public void Delete(FileRecord entity)
    {
        _ = entity ?? throw new ArgumentNullException(nameof(entity));
        using var repository = CreateRepositoryForDay(entity.DateTaken.Date);
        repository.Delete(entity);
    }

    public IEnumerable<FileRecord> GetRecords(DateTime startDate, DateTime endDate, int? count)
    {
        var random = new Random();
        var records = new List<FileRecord>();

        for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
        {
            var fileName = GetFileName(date) + ".db";
            if (!File.Exists(
                    Path.Combine(
                        _settings.FileRecordsFolder,
                        fileName)))
            {
                continue;
            }

            using var repository = CreateRepositoryForDay(date);
            var repositoryRecords = repository.GetAll().Where(
                x => !x.Id.Contains(
                    "$RECYCLE.BIN",
                    StringComparison.OrdinalIgnoreCase));
            records.AddRange(repositoryRecords);
        }

        IEnumerable<FileRecord> randomlyOrdered = count == null ? records : records.OrderBy(x => random.Next());
        var i = 0;

        foreach (var record in randomlyOrdered)
        {
            if (File.Exists(record.Id))
            {
                i++;
                yield return record;
            }

            if (i == count)
            {
                yield break;
            }
        }
    }

    public YearRange? GetYears()
    {
        var repositoryFiles = Directory.GetFiles(_settings.FileRecordsFolder);
        if (repositoryFiles.Length == 0)
        {
            return null; // No files found, return null
        }

        var years = repositoryFiles.Select(file =>
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            var yearPart = fileName[
                ..4]; // Extract year part from file name
            if (int.TryParse(yearPart, out var year))
            {
                return year;
            }

            return (int?)null;
        }).Where(year => year != null).Select(year => year).Cast<int>().ToList();

        if (years.Count == 0)
        {
            return null; // No valid years found, return null
        }

        var minYear = years.Min();
        var maxYear = years.Max();

        return new YearRange(minYear, maxYear);
    }

    public FileRecordRepository CreateRepositoryForDay(DateTime date)
    {
        var dayPartition = GetFileName(date);
        return new FileRecordRepository(_settings.FileRecordsFolder, dayPartition);
    }

    static string GetFileName(DateTime date)
    {
        var dayPartition = date.ToString(
            "yyyyMMdd",
            CultureInfo.InvariantCulture);
        return dayPartition;
    }
}