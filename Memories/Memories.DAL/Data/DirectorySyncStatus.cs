using Scar.Common.DAL.Contracts.Model;

namespace PhotoReviewer.Memories.DAL.Data;

public class DirectorySyncStatus : Entity<string>
{
    public long DateModifiedTicks { get; set; }
}