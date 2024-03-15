using Microsoft.Extensions.Logging;
using PhotoReviewer.Memories.DAL;
using PhotoReviewer.Memories.DAL.Data;

namespace PhotoReviewer.Memories.DataUpdater;

public static class Program
{
    static async Task Main()
    {
        Console.WriteLine("Started");
        var settings = new Settings();

        const string dataFolder = "FileRecords";
        using var loggerFactory = new LoggerFactory();
        var logger = new Logger<DirectorySyncStatusRepository>(loggerFactory);
        var path = Path.Combine(
            settings.DataFolder,
            dataFolder);
        var files = Directory.EnumerateFiles(path).Where(
            x => !x.EndsWith(
                "-log.db",
                StringComparison.CurrentCulture));
        var currentFile = 0;
        var fileTasks = files.Select(
            filePath => Task.Run(
                () =>
                {
                    currentFile++;

                    try
                    {
                        using var realEstateDescriptionRepository = new DirectorySyncStatusRepository(settings);

                        Console.WriteLine($"{currentFile}: Initialized RealEstateDescriptionRepository for {filePath}");
                        var entities = realEstateDescriptionRepository.GetAll();

                        var toDelete = new List<DirectorySyncStatus>();
                        var toUpdate = new List<DirectorySyncStatus>();
                        foreach (var entity in entities)
                        {
                            if (ShouldDelete(entity))
                            {
                                toDelete.Add(entity);
                            }
                            else
                            {
                                var fine = Update(entity);
                                if (fine)
                                {
                                    toUpdate.Add(entity);
                                }
                                else
                                {
                                    toDelete.Add(entity);
                                }
                            }
                        }

                        if (toDelete.Count > 0)
                        {
                            realEstateDescriptionRepository.Delete(toDelete);
                        }

                        if (toUpdate.Count > 0)
                        {
                            realEstateDescriptionRepository.Update(toUpdate);
                        }
                    }
                    catch
                    {
                        Console.Error.WriteLine("Cannot process " + path);
                    }
                }));
        await Task.WhenAll(fileTasks).ConfigureAwait(false);
    }

    static bool Update(DirectorySyncStatus entity)
    {
        entity.Id = entity.Id;
        return true;
    }

    static bool ShouldDelete(DirectorySyncStatus entity)
    {
        return Directory.Exists(entity.Id);
    }

    sealed class Settings : IRepositorySettings
    {
        public string DataFolder => "X:\\Data.Memories";

        public string FileRecordsFolder => "Z:\\";
    }
}