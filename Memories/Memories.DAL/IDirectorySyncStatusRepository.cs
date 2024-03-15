using PhotoReviewer.Memories.DAL.Data;
using Scar.Common.DAL.Contracts;

namespace PhotoReviewer.Memories.DAL;

public interface IDirectorySyncStatusRepository : IRepository<DirectorySyncStatus, string>, IDisposable, IFileBasedRepository;