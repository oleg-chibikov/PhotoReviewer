using Quartz;

namespace PhotoReviewer.Memories.Core;

public class MemoriesJob(NotificationManager notificationManager) : IJob
{
    public Task Execute(IJobExecutionContext context)
    {
        notificationManager.ShowNotification("Memories from a year ago", "View your memories");

        return Task.CompletedTask;
    }
}