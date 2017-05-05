using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace PhotoReviewer.Core
{
    public interface ICancellationTokenSourceProvider
    {
        CancellationToken Token { get; }

        void Cancel();

        Task StartNewTask([NotNull]Action<CancellationToken> action, bool cancelCurrent = true, bool runInTheSameSynchronizationContext = false);

        Task CurrentTask { get; }
    }
}