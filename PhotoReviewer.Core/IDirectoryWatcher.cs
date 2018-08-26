using System;
using JetBrains.Annotations;
using Scar.Common.Events;
using Scar.Common.Notification;

namespace PhotoReviewer.Core
{
    public interface IDirectoryWatcher : INotificationSupressable
    {
        event EventHandler<EventArgs<string>> FileAdded;
        event EventHandler<EventArgs<string>> FileDeleted;
        event EventHandler<EventArgs<Tuple<string, string>>> FileRenamed;

        void SetDirectoryPath([NotNull] string directoryPath);
    }
}