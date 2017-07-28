using System;
using System.IO;
using Common.Logging;
using JetBrains.Annotations;
using Scar.Common.Notification;

namespace PhotoReviewer.Core
{
    public class FileSystemWatcherNotificationSupresser : NotificationSupresser
    {
        [NotNull]
        private readonly FileSystemWatcher _fileSystemWatcher;

        [NotNull]
        private readonly ILog _logger;

        public FileSystemWatcherNotificationSupresser([NotNull] INotificationSupressable notificationSupressable, [NotNull] FileSystemWatcher fileSystemWatcher, [NotNull] ILog logger)
            : base(notificationSupressable)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _fileSystemWatcher = fileSystemWatcher ?? throw new ArgumentNullException(nameof(fileSystemWatcher));
            _logger.Trace("Supressing file system events...");
            _fileSystemWatcher.EnableRaisingEvents = false;
        }

        public override void Dispose()
        {
            base.Dispose();
            _logger.Trace("Restoring file system events...");
            _fileSystemWatcher.EnableRaisingEvents = true;
        }
    }
}