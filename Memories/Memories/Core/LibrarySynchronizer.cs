using System.IO;
using Microsoft.Extensions.Logging;
using PhotoReviewer.Memories.DAL;
using PhotoReviewer.Memories.DAL.Data;
using Scar.Common.ImageProcessing.MetadataExtraction;

namespace PhotoReviewer.Memories.Core;

public class LibrarySynchronizer(IFileRecordRepositoryFactory fileRecordRepositoryFactory, IDirectorySyncStatusRepository directorySyncStatusRepository, ILogger<LibrarySynchronizer> logger, IMetadataExtractor metadataExtractor) : IDisposable
{
    readonly ILogger<LibrarySynchronizer> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    readonly IFileRecordRepositoryFactory _fileRecordRepositoryFactory = fileRecordRepositoryFactory ?? throw new ArgumentNullException(nameof(fileRecordRepositoryFactory));
    readonly IDirectorySyncStatusRepository _directorySyncStatusRepository = directorySyncStatusRepository ?? throw new ArgumentNullException(nameof(directorySyncStatusRepository));
    readonly IMetadataExtractor _metadataExtractor = metadataExtractor ?? throw new ArgumentNullException(nameof(metadataExtractor));
    readonly HashSet<string> _ignoredDirectoryNames = new() { "$RECYCLE.BIN", "Screenshots", "!Big Events", "!Various", "@Recently-Snapshot", "@Recycle" };
    readonly AlphanumericComparer _alphanumericComparer = new();

    public async Task SyncAsync(string rootFolder)
    {
        try
        {
            await SyncInitialFilesAsync(rootFolder).ConfigureAwait(false);
        }
        finally
        {
            _directorySyncStatusRepository.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _directorySyncStatusRepository.Dispose();
        }
    }

    async IAsyncEnumerable<FileRecord> GetFilesAsync(string path)
    {
        foreach (var filePath in Directory.EnumerateFiles(path).Where(x => MetadataExtractor.ImageExtensions.Contains(Path.GetExtension(x).ToLowerInvariant())))
        {
            var metadata = await _metadataExtractor.ExtractAsync(filePath, MetadataOptions.DateTaken).ConfigureAwait(false);
            var fileRecord = FileRecord.Create(filePath, metadata);
            yield return fileRecord;
        }
    }

    async Task SyncInitialFilesAsync(string path)
    {
        if (_ignoredDirectoryNames.Contains(Path.GetFileNameWithoutExtension(path)))
        {
            return;
        }

        _logger.LogInformation("Syncing {Path}...", path);

        var directoryInfo = new DirectoryInfo(path);
        var storedLastModifiedDateTicks = _directorySyncStatusRepository.TryGetById(path)?.DateModifiedTicks;
        var currentLastModifiedDateTicks = directoryInfo.LastWriteTime.Ticks;
        if (storedLastModifiedDateTicks == null || currentLastModifiedDateTicks > storedLastModifiedDateTicks)
        {
            var files = GetFilesAsync(path).ToBlockingEnumerable().ToList();
            if (files.Count > 0)
            {
                _fileRecordRepositoryFactory.Upsert(files);
            }

            _directorySyncStatusRepository.Upsert(
                new DirectorySyncStatus
                    { Id = path, DateModifiedTicks = currentLastModifiedDateTicks });
            _directorySyncStatusRepository.Persist();
        }
        else
        {
            _logger.LogInformation("Skipped checking files of {Path} as it was not modified since last time", path);
        }

        foreach (var directory in Directory.GetDirectories(path).OrderBy(x => x, _alphanumericComparer))
        {
            await SyncInitialFilesAsync(directory).ConfigureAwait(false);
        }

        _logger.LogInformation("Synced {Path}", path);
    }
}