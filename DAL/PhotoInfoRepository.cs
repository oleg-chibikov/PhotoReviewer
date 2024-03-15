using Microsoft.Extensions.Logging;
using PhotoReviewer.Contracts.DAL;
using PhotoReviewer.Contracts.DAL.Data;
using PhotoReviewer.DAL.Model;
using Scar.Common.ApplicationLifetime.Contracts;
using Scar.Common.DAL.LiteDB;

namespace PhotoReviewer.DAL;

public sealed class PhotoInfoRepository<TPhotoInfo> : LiteDbRepository<TPhotoInfo, FileLocation>, IPhotoInfoRepository<TPhotoInfo>
    where TPhotoInfo : PhotoInfo, new()
{
    readonly ILogger _logger;

    public PhotoInfoRepository(ILogger<PhotoInfoRepository<TPhotoInfo>> logger, IAssemblyInfoProvider assemblyInfoProvider)
        : base(assemblyInfoProvider?.SettingsPath ?? throw new ArgumentNullException(nameof(assemblyInfoProvider)))
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // TODO: This index cannot handle keys more than 512 bytes. So that filepath length is limited. Maybe store hash of filepath to find photos
        Collection.EnsureIndex(x => x.Id.Directory);
        Task.Run(CleanNonExisting);
    }

    public IEnumerable<TPhotoInfo> GetByDirectory(string directoryPath)
    {
        _logger.LogTrace("Getting all files by directory {DirectoryPath}...", directoryPath);
        ArgumentNullException.ThrowIfNull(directoryPath);

        return Collection.Find(x => x.Id.Directory.Equals(directoryPath, StringComparison.OrdinalIgnoreCase));
    }

    public void Rename(FileLocation oldFileLocation, FileLocation newFileLocation)
    {
        _logger.LogDebug("Renaming {OldFileLocation} to {NewFileLocation} in the database...", oldFileLocation, newFileLocation);
        if (!Check(oldFileLocation))
        {
            _logger.LogWarning("There is no {OldFileLocation} in the database...", oldFileLocation);
            return;
        }

        Delete(oldFileLocation);
        Upsert(
            new TPhotoInfo
            {
                Id = newFileLocation
            });
    }

    void CleanNonExisting()
    {
        var notExisting = GetAll().Where(photoInfo => !File.Exists(photoInfo.Id.ToString())).ToArray();
        if (!(notExisting.Length > 0))
        {
            _logger.LogTrace("All records are up to date");
            return;
        }

        _logger.LogDebug("Deleting {NotExistingLength} non existing photos from the database...", notExisting.Length);
        Delete(notExisting);
    }
}