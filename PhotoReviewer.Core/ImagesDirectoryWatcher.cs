using System;
using System.IO;
using System.Linq;
using Common.Logging;
using JetBrains.Annotations;
using PhotoReviewer.Resources;
using Scar.Common.Events;
using Scar.Common.Notification;

namespace PhotoReviewer.Core
{
    [UsedImplicitly]
    internal sealed class ImagesDirectoryWatcher : IDirectoryWatcher, IDisposable
    {
        [NotNull]
        private readonly FileSystemWatcher _fileSystemWatcher = new FileSystemWatcher
        {
            //TODO: polling every n seconds or use queue for handlers
            InternalBufferSize = 64 * 1024
        };

        [NotNull]
        private readonly ILog _logger;
        //TODO: IsImage needs to be moved as an abstract
        //TODO: Library (IO)

        public ImagesDirectoryWatcher([NotNull] ILog logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _fileSystemWatcher.Created += FileSystemWatcher_Changed;
            _fileSystemWatcher.Deleted += FileSystemWatcher_Changed;
            _fileSystemWatcher.Renamed += FileSystemWatcher_Renamed;
        }

        public event EventHandler<EventArgs<string>> FileAdded;
        public event EventHandler<EventArgs<string>> FileDeleted;
        public event EventHandler<EventArgs<Tuple<string, string>>> FileRenamed;

        public void SetDirectoryPath(string directoryPath)
        {
            using (SupressNotification())
            {
                _fileSystemWatcher.Path = directoryPath;
            }
        }

        [NotNull]
        public NotificationSupresser SupressNotification()
        {
            //TODO: DI
            return new FileSystemWatcherNotificationSupresser(this, _fileSystemWatcher, _logger);
        }

        public bool NotificationIsSupressed { get; set; }

        public void Dispose()
        {
            _fileSystemWatcher.Dispose();
            _fileSystemWatcher.Created -= FileSystemWatcher_Changed;
            _fileSystemWatcher.Deleted -= FileSystemWatcher_Changed;
            _fileSystemWatcher.Renamed -= FileSystemWatcher_Renamed;
            //Unsubscribe all
            FileAdded = null;
            FileRenamed = null;
            FileDeleted = null;
        }

        private void FileSystemWatcher_Changed([NotNull] object sender, [NotNull] FileSystemEventArgs fileSystemEventArgs)
        {
            _logger.Info($"File system event received: {fileSystemEventArgs.ChangeType}: {fileSystemEventArgs.Name}, {fileSystemEventArgs.FullPath}");
            var filePath = fileSystemEventArgs.FullPath;
            if (!IsImage(filePath))
            {
                _logger.Trace($"{fileSystemEventArgs.FullPath} is not considered to be an image");
                return;
            }

            switch (fileSystemEventArgs.ChangeType)
            {
                case WatcherChangeTypes.Deleted:
                    FileDeleted?.Invoke(this, new EventArgs<string>(filePath));
                    break;
                case WatcherChangeTypes.Created:
                    FileAdded?.Invoke(this, new EventArgs<string>(filePath));
                    break;
            }
        }

        private void FileSystemWatcher_Renamed([NotNull] object sender, [NotNull] RenamedEventArgs renamedEventArgs)
        {
            _logger.Info($"File system event received: {renamedEventArgs.ChangeType}: {renamedEventArgs.Name}, {renamedEventArgs.FullPath}");
            if (!IsImage(renamedEventArgs.FullPath))
            {
                _logger.Trace($"{renamedEventArgs.FullPath} is not considered to be an image");
                return;
            }

            FileRenamed?.Invoke(this, new EventArgs<Tuple<string, string>>(new Tuple<string, string>(renamedEventArgs.OldFullPath, renamedEventArgs.FullPath)));
        }

        private static bool IsImage(string filePath)
        {
            var extenstion = Path.GetExtension(filePath);
            return extenstion != null && Constants.FileExtensions.Contains(extenstion, StringComparer.InvariantCultureIgnoreCase);
        }
    }
}