using Scar.Common.Events;
using Scar.Common.Notification;

namespace PhotoReviewer.Core;

public interface IDirectoryWatcher : INotificationSuppressable
{
    event EventHandler<EventArgs<string>> FileAdded;

    event EventHandler<EventArgs<string>> FileDeleted;

    event EventHandler<EventArgs<FileRenamedArgs>> FileRenamed;

    void SetDirectoryPath(string directoryPath);
}