using PhotoReviewer.Memories.DAL.Data;
using Scar.Common.DAL.LiteDB;

namespace PhotoReviewer.Memories.DAL;

public class DirectorySyncStatusRepository : LiteDbRepository<DirectorySyncStatus, string>,
    IDirectorySyncStatusRepository
{
    public DirectorySyncStatusRepository(IRepositorySettings settings) : base(
        settings?.DataFolder ?? throw new ArgumentNullException(nameof(settings)), null, true, true
    )
    {
        Collection.EnsureIndex(x => x.Id);
    }
}