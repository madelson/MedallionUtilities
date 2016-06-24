using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Medallion.Async
{
    public sealed class AsyncLock
    {
        private readonly SemaphoreSlim semaphore;

        public AsyncLock()
        {
            this.semaphore = new SemaphoreSlim(initialCount: 1, maxCount: 1);
        }

        public AwaitableHandle<IDisposable> AcquireAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return new AwaitableHandle<IDisposable>(this.InternalAcquireAsync(cancellationToken));
        }

        private async Task<IDisposable> InternalAcquireAsync(CancellationToken cancellationToken)
        {
            await this.semaphore.WaitAsync(cancellationToken);

            return new LockHandle(this);
        }

        public struct LockHandleOption : IDisposable
        {
            internal LockHandleOption(AsyncLock @lock)
            {

            }

            public void Dispose()
            {
                throw new NotImplementedException();
            }
        }

        private sealed class LockHandle : IDisposable
        {
            private AsyncLock @lock;
            private int state;

            public LockHandle(AsyncLock @lock) { this.@lock = @lock; }

            void IDisposable.Dispose()
            {
                var @lock = Interlocked.Exchange(ref this.@lock, null);
                if (@lock != null)
                {
                    @lock.semaphore.Release();
                }
            }
        }

        /// <summary>
        /// Based on https://github.com/StephenCleary/AsyncEx/blob/master/Source/Nito.AsyncEx%20(NET45%2C%20Win8%2C%20WP8%2C%20WPA81)/AwaitableDisposable.cs
        /// </summary>
        public struct AwaitableHandle<THandle>
        {
            private readonly Task<THandle> task;

            internal AwaitableHandle(Task<THandle> task)
            {
                this.task = task;
            }

            public Task<THandle> Task => this.task;

            public TaskAwaiter<THandle> GetAwaiter() => this.task.GetAwaiter();

            public ConfiguredTaskAwaitable<THandle> ConfigureAwait(bool continueOnCapturedContext) => this.task.ConfigureAwait(continueOnCapturedContext);
        }
    }
}
