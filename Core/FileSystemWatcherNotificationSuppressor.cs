using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Scar.Common.Notification;

namespace PhotoReviewer.Core
{
    public sealed class FileSystemWatcherNotificationSuppressor : NotificationSuppressor
    {
        readonly FileSystemWatcher _fileSystemWatcher;

        readonly ILogger _logger;

        // TODO: Instantiate with DI and pass generic logger
        public FileSystemWatcherNotificationSuppressor(
            INotificationSuppressable notificationSuppressable,
            FileSystemWatcher fileSystemWatcher,
            ILogger logger)
            : base(notificationSuppressable)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _fileSystemWatcher = fileSystemWatcher ?? throw new ArgumentNullException(nameof(fileSystemWatcher));
            _logger.LogTrace("Suppressing file system events...");
            _fileSystemWatcher.EnableRaisingEvents = false;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                _logger.LogTrace("Restoring file system events...");
                _fileSystemWatcher.EnableRaisingEvents = true;
            }
        }
    }
}
