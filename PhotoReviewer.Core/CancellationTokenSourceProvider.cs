using System;
using System.Threading;
using System.Threading.Tasks;
using Easy.MessageHub;
using JetBrains.Annotations;
using PhotoReviewer.Resources;
using Scar.Common.Messages;

namespace PhotoReviewer.Core
{
    [UsedImplicitly]
    internal sealed class CancellationTokenSourceProvider : IDisposable, ICancellationTokenSourceProvider
    {
        //TODO: Remove dependency from messenger and move to library
        [NotNull]
        private readonly IMessageHub _messenger;

        [NotNull]
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public CancellationTokenSourceProvider([NotNull] IMessageHub messenger)
        {
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        }

        public Task CurrentTask { get; private set; } = Task.CompletedTask;

        public CancellationToken Token => _cancellationTokenSource.Token;

        public async Task StartNewTask(Action<CancellationToken> action, bool cancelCurrent)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            await ExecuteAsyncOperation(token => Task.Run(() => action(token), token)).ConfigureAwait(false);
        }

        public async Task ExecuteAsyncOperation(Func<CancellationToken, Task> func, bool cancelCurrent = true)
        {
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            if (!cancelCurrent && !CurrentTask.IsCompleted)
            {
                _messenger.Publish(Errors.TaskInProgress.ToWarning());
                return;
            }

            var newCts = new CancellationTokenSource();
            var oldCts = Interlocked.Exchange(ref _cancellationTokenSource, newCts);
            oldCts?.Cancel();
            var token = newCts.Token;
            CurrentTask = func(token);
            await CurrentTask.ConfigureAwait(false);
        }

        public void Cancel()
        {
            _cancellationTokenSource.Cancel();
        }

        public void Dispose()
        {
            _cancellationTokenSource.Dispose();
        }
    }
}