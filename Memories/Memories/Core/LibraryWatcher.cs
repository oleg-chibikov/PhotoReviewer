using System.IO;
using Microsoft.Extensions.Logging;
using PhotoReviewer.Memories.DAL;
using PhotoReviewer.Memories.DAL.Data;
using PhotoReviewer.Memories.Data;
using Scar.Common.ImageProcessing.MetadataExtraction;

namespace PhotoReviewer.Memories.Core;

public class LibraryWatcher(Settings settings, IMetadataExtractor metadataExtractor, ILogger<LibrarySynchronizer> logger, IFileRecordRepositoryFactory fileRecordRepositoryFactory) : IDisposable
{
    FileSystemWatcher? _fileSystemWatcher;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public void Watch()
    {
        _fileSystemWatcher = new FileSystemWatcher(settings.LibraryFolder)
        {
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };

        _fileSystemWatcher.Created += async (_, e) => await OnFileEventAsync(e.FullPath, x =>
        {
            logger.LogInformation("Created new file {Path}", e.FullPath);
            fileRecordRepositoryFactory.Upsert(x);
        }).ConfigureAwait(true);
        _fileSystemWatcher.Deleted += async (_, e) => await OnFileEventAsync(e.FullPath, x =>
        {
            logger.LogInformation("Deleted file {Path}", e.FullPath);
            fileRecordRepositoryFactory.Delete(x);
        }).ConfigureAwait(true);
        _fileSystemWatcher.Renamed += async (_, e) => await OnFileRenamedAsync(e.OldFullPath, e.FullPath).ConfigureAwait(true);
        logger.LogInformation("Watching for changes in {Path}", settings.LibraryFolder);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _fileSystemWatcher?.Dispose();
        }
    }

    async Task OnFileEventAsync(string filePath, Action<FileRecord> action)
    {
        // TODO: Check Directory event
        var metadata = await metadataExtractor.ExtractAsync(filePath, MetadataOptions.DateTaken).ConfigureAwait(false);
        var fileRecord = FileRecord.Create(filePath, metadata);
        action(fileRecord);
    }

    async Task OnFileRenamedAsync(string oldPath, string newPath)
    {
        // TODO: Check Directory event
        logger.LogInformation("Renamed file {OldPath} to {Path}", oldPath, newPath);
        var metadata = await metadataExtractor.ExtractAsync(newPath, MetadataOptions.DateTaken).ConfigureAwait(false);
        var fileRecord = FileRecord.Create(newPath, metadata);
        var oldFileRecord = FileRecord.Create(oldPath, metadata);
        fileRecordRepositoryFactory.Upsert(fileRecord);
        fileRecordRepositoryFactory.Delete(oldFileRecord);
    }
}