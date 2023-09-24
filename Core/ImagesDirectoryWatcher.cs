using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using PhotoReviewer.Resources;
using Scar.Common.Events;
using Scar.Common.Notification;

namespace PhotoReviewer.Core
{
    public sealed class ImagesDirectoryWatcher : IDirectoryWatcher, IDisposable
    {
        readonly FileSystemWatcher _fileSystemWatcher = new ()
        {
            // TODO: polling every n seconds or use queue for handlers
            InternalBufferSize = 64 * 1024
        };

        readonly ILogger _logger;

        // TODO: IsImage needs to be moved as an abstract
        // TODO: Library (IO)
        public ImagesDirectoryWatcher(ILogger<ImagesDirectoryWatcher> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _fileSystemWatcher.Created += FileSystemWatcher_Changed;
            _fileSystemWatcher.Deleted += FileSystemWatcher_Changed;
            _fileSystemWatcher.Renamed += FileSystemWatcher_Renamed;
        }

        public event EventHandler<EventArgs<string>>? FileAdded;

        public event EventHandler<EventArgs<string>>? FileDeleted;

        public event EventHandler<EventArgs<FileRenamedArgs>>? FileRenamed;

        public bool NotificationIsSuppressed { get; set; }

        public void SetDirectoryPath(string directoryPath)
        {
            using (SuppressNotification())
            {
                _fileSystemWatcher.Path = directoryPath;
            }
        }

        public NotificationSuppressor SuppressNotification()
        {
            // TODO: DI
            return new FileSystemWatcherNotificationSuppressor(this, _fileSystemWatcher, _logger);
        }

        public void Dispose()
        {
            _fileSystemWatcher.Dispose();
            _fileSystemWatcher.Created -= FileSystemWatcher_Changed;
            _fileSystemWatcher.Deleted -= FileSystemWatcher_Changed;
            _fileSystemWatcher.Renamed -= FileSystemWatcher_Renamed;

            // Unsubscribe all
            FileAdded = null;
            FileRenamed = null;
            FileDeleted = null;
        }

        static bool IsImage(string filePath)
        {
            var extenstion = Path.GetExtension(filePath);
            return extenstion != null && Constants.FileExtensions.Contains(extenstion, StringComparer.InvariantCultureIgnoreCase);
        }

        void FileSystemWatcher_Changed(object? sender, FileSystemEventArgs fileSystemEventArgs)
        {
            if (CheckImage(fileSystemEventArgs, out var filePath))
            {
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

        void FileSystemWatcher_Renamed(object? sender, RenamedEventArgs renamedEventArgs)
        {
            if (CheckImage(renamedEventArgs, out var filePath))
            {
                return;
            }

            FileRenamed?.Invoke(this, new EventArgs<FileRenamedArgs>(new FileRenamedArgs(renamedEventArgs.OldFullPath, renamedEventArgs.FullPath)));
        }

        bool CheckImage(FileSystemEventArgs fileSystemEventArgs, out string filePath)
        {
            _logger.LogInformation(
                "File system event received: {ChangeType}: {Name}, {FullPath}",
                fileSystemEventArgs.ChangeType,
                fileSystemEventArgs.Name,
                fileSystemEventArgs.FullPath);
            filePath = fileSystemEventArgs.FullPath;
            if (!IsImage(filePath))
            {
                _logger.LogTrace("{FullPath} is not considered to be an image", fileSystemEventArgs.FullPath);
                return true;
            }

            return false;
        }
    }
}
