using PhotoReviewer.Memories.Data;
using Quartz;

namespace PhotoReviewer.Memories.Core;

public class SynchronizerJob(Settings settings, LibrarySynchronizer librarySynchronizer) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        await librarySynchronizer.SyncAsync(settings.LibraryFolder).ConfigureAwait(false);
    }
}