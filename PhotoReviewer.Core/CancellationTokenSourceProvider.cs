using System;
using System.Threading;
using System.Threading.Tasks;
using GalaSoft.MvvmLight.Messaging;
using JetBrains.Annotations;
using PhotoReviewer.Resources;

namespace PhotoReviewer.Core
{
    [UsedImplicitly]
    internal sealed class CancellationTokenSourceProvider : IDisposable, ICancellationTokenSourceProvider
    {
        [NotNull]
        private readonly IMessenger _messenger;

        [NotNull]
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public CancellationTokenSourceProvider([NotNull] IMessenger messenger)
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
                _messenger.Send(Errors.TaskInProgress, MessengerTokens.UserWarningToken);
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