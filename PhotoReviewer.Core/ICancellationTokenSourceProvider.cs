using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace PhotoReviewer.Core
{
    public interface ICancellationTokenSourceProvider
    {
        CancellationToken Token { get; }

        Task CurrentTask { get; }

        void Cancel();

        [NotNull]
        Task StartNewTask([NotNull] Action<CancellationToken> action, bool cancelCurrent = true);

        [NotNull]
        Task ExecuteAsyncOperation([NotNull] Func<CancellationToken,Task> func, bool cancelCurrent = true);
    }
}