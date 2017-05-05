using System;
using System.Threading;
using System.Threading.Tasks;
using GalaSoft.MvvmLight.Messaging;
using JetBrains.Annotations;
using PhotoReviewer.Resources;

namespace PhotoReviewer.Core
{
    internal sealed class CancellationTokenSourceProvider : IDisposable, ICancellationTokenSourceProvider
    {
        [NotNull]
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        [NotNull] private readonly IMessenger _messenger;
        public void Dispose()
        {
            _cancellationTokenSource.Dispose();
        }

        public Task CurrentTask { get; private set; } = Task.CompletedTask;

        public CancellationTokenSourceProvider([NotNull] IMessenger messenger)
        {
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        }

        public CancellationToken Token => _cancellationTokenSource.Token;

        public async Task StartNewTask(Action<CancellationToken> action, bool cancelCurrent,
            bool runInCurrentSyncContext)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (!cancelCurrent && !CurrentTask.IsCompleted)
            {
                _messenger.Send(Errors.TaskInProgress, MessengerTokens.UserWarningToken);
                return;
            }
            var newCts = new CancellationTokenSource();
            var oldCts = Interlocked.Exchange(ref _cancellationTokenSource, newCts);
            oldCts?.Cancel();
            var token = newCts.Token;
            CurrentTask = Task.Factory.StartNew(
                () => action(token),
                token,
                TaskCreationOptions.None,
                runInCurrentSyncContext
                    ? TaskScheduler.FromCurrentSynchronizationContext()
                    : TaskScheduler.Default);
            await CurrentTask;
        }


        public void Cancel()
        {
            _cancellationTokenSource.Cancel();
        }
    }
}
