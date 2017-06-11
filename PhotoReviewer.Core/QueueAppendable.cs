using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace PhotoReviewer.Core
{
    //TODO: Library
    [UsedImplicitly]
    public sealed class QueueAppendable : IAppendable<Func<Task>>, IDisposable
    {
        private readonly BlockingCollection<Func<Task>> _queue = new BlockingCollection<Func<Task>>();

        public QueueAppendable()
        {
            Task.Factory.StartNew(
                async () =>
                {
                    while (true)
                        try
                        {
                            var task = _queue.Take();
                            await task().ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            //TODO: log
                        }
                        catch (InvalidOperationException)
                        {
                            break;
                        }
                        catch
                        {
                            // TODO log me
                        }
                },
                TaskCreationOptions.LongRunning);
        }

        public int CurrentlyQueuedTasks => _queue.Count;

        public void Append([NotNull] Func<Task> task)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));
            _queue.Add(task);
        }

        public void Dispose()
        {
            _queue.CompleteAdding();
        }
    }
}