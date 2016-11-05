using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.AsyncLinq
{
    // ideas:
    // yield(asyncenumerable), but this returns task and just loops w/yield?
    // yield(enumerable), stores sequence internally

    public sealed class AsyncIteratorBuilder<T> : IAsyncEnumerator<T>
    {
        private readonly object @lock = new object();

        private State state = State.NotStarted;
        private Func<AsyncIteratorBuilder<T>, Task> asyncIteratorFunc;
        private TaskCompletionSource<bool> moveNextTaskBuilder;
        private T currentValue;
        private IEnumerator<T> currentEnumerator;
        private Action yieldContinuation;
        private Exception yieldedEnumeratorOrCompletionException;

        internal AsyncIteratorBuilder(Func<AsyncIteratorBuilder<T>, Task> asyncIteratorFunc)
        {
            if (asyncIteratorFunc == null) { throw new ArgumentNullException(nameof(asyncIteratorFunc)); }

            this.asyncIteratorFunc = asyncIteratorFunc;
        }

        #region ---- Builder API ----
        #endregion

        #region ---- IAsyncEnumerator ----
        T IAsyncEnumerator<T>.Current
        {
            get
            {
                lock (this.@lock)
                {
                    switch (this.state)
                    {
                        case State.NotStarted:
                            throw new InvalidOperationException($"The enumeration has not started: call '{nameof(IAsyncEnumerator<T>.MoveNextAsync)}'");
                        case State.WaitingForSyncYieldOrCompletion:
                        case State.WaitingForAsyncYieldOrCompletion:
                            throw ConcurrentAccessViolation();
                        case State.Idle:
                        case State.IdleWithEnumerator:
                            return this.currentValue;
                        case State.Completed:
                            throw new InvalidOperationException("The enumeration has already finished");
                        case State.Disposed:
                            throw this.Disposed();
                        default:
                            throw this.UnexpectedState();
                    }
                }
            }
        }

        Task<bool> IAsyncEnumerator<T>.MoveNextAsync()
        {
            lock (this.@lock)
            {
                while (true)
                {
                    var task = this.MoveNextAsyncHelperNoLock();
                    if (task != null) { return task; }
                }
            }
        }

        private Task<bool> MoveNextAsyncHelperNoLock()
        {
            switch (this.state)
            {
                case State.NotStarted:
                    // attempt to start the iterator
                    this.state = State.WaitingForSyncYieldOrCompletion;
                    Task task;
                    try { task = this.asyncIteratorFunc(this); }
                    catch (Exception ex) { return this.SetErrorStateNoLock(ex); }
                    
                    // todo probably remove this
                    // first, check for synchronous completion. This is an unusual case
                    // which can mean a few different things
                    if (task.IsCompleted)
                    {
                        if (this.state != State.WaitingForSyncYieldOrCompletion)
                        {
                            // this means that the task completed synchronously but changed 
                            // the state. In other words, it yielded but didn't await the yield
                            return this.SetErrorStateNoLock(ConcurrentAccessViolation());
                        }

                        // this means that the task finished synchronously without yielding
                        this.state = State.Completed;
                        // if the task failed, then we return a faulted/canceled task. Note that this means
                        // that only one call to MoveNextAsync() will fail. This is consistent with native yield return iterators
                        if (task.IsCanceled) { return AsyncEnumerable.CanceledTask; }
                        if (task.IsFaulted) { return Task.FromException<bool>(Unwrap(task.Exception)); }
                        return null; // go around again and hit the normal Completed handler
                    }

                    // otherwise, have the iterator terminate when the task finishes
                    task.ContinueWith(AsyncIteratorFuncContinuation, state: this, continuationOptions: TaskContinuationOptions.ExecuteSynchronously);

                    // signal the caller to retry
                    return null;
                case State.WaitingForSyncYieldOrCompletion:
                    // this means that we just ran the iterator func or a yield continuation.
                    // Either that will complete synchronously and yield/finish, which will change
                    // the state OR it will go async and we'll get back here in the waiting for sync
                    // state. In that case, we switch to waiting for async and return an incomplete task
                    this.state = State.WaitingForAsyncYieldOrCompletion;
                    this.moveNextTaskBuilder = new TaskCompletionSource<bool>();
                    return this.moveNextTaskBuilder.Task;
                case State.WaitingForAsyncYieldOrCompletion:
                    // this means that someone called MoveNextAsync() before the previous MoveNextAsync call completed
                    throw ConcurrentAccessViolation();
                case State.Idle:
                    // this means that we're not waiting for anything. We should have a yield continuation 
                    // though, so invoke that
                    return this.RunYieldContinuationNoLock();
                case State.IdleWithEnumerator:
                    // this means that we potentially have more values queued up in a synchronous enumerator.
                    // We'll try to pull one from there if we can
                    try
                    {
                        if (this.currentEnumerator.MoveNext())
                        {
                            this.currentValue = this.currentEnumerator.Current;
                            return AsyncEnumerable.TrueTask; // complete synchronously
                        }
                        
                        // enumerator is exhausted, which puts us in the idle state
                        this.state = State.Idle;
                        this.currentEnumerator.Dispose();
                        return null; // go around again
                    }
                    catch (Exception ex)
                    {
                        // this is an interesting case: it means that the enumerator either failed to
                        // advance or dispose. Note that this should NOT put us in the error case: the
                        // async iterator could catch this and yield something else instead. Thus, we
                        // simply run the yield continuation and have it throw the exception
                        this.yieldedEnumeratorOrCompletionException = ex;
                        try { this.currentEnumerator.Dispose(); }
                        catch (Exception disposeException) { this.yieldedEnumeratorOrCompletionException = disposeException; }
                        return this.RunYieldContinuationNoLock();
                    }
                case State.Completed:
                    var completionException = this.yieldedEnumeratorOrCompletionException;
                    if (completionException != null)
                    {
                        // this means that further MoveNext calls will simply return false. This is
                        // consistent with how native yield iterators work
                        this.yieldedEnumeratorOrCompletionException = null;
                        return Task.FromException<bool>(completionException);
                    }

                    return AsyncEnumerable.FalseTask;
                case State.Error:
                    // in this case, the error has been stored in the task builder
                    return this.moveNextTaskBuilder.Task;
                case State.Disposed:
                    throw this.Disposed();
                default:
                    throw this.UnexpectedState();
            }
        }

        private static readonly Action<Task, object> AsyncIteratorFuncContinuation = (task, state) =>
        {
            var @this = (AsyncIteratorBuilder<T>)state;
            lock (@this.@lock)
            {
                switch (@this.state)
                {
                    case State.WaitingForSyncYieldOrCompletion:
                        // this means that the async iterator func task completed synchronously during MoveNextAsync().
                        // We thus store off any exception and transition to the completed state
                        @this.state = State.Completed;
                        @this.yieldedEnumeratorOrCompletionException = task.IsCanceled ? new OperationCanceledException()
                            : task.IsFaulted ? Unwrap(task.Exception)
                            : null;
                        break;
                    case State.WaitingForAsyncYieldOrCompletion:
                        // this means that the async iterator func task completed asynchronously and thus needs to finish
                        // off the MoveNextAsync task. Do that, propagating cancellation/exceptions as necessary
                        @this.state = State.Completed;
                        if (task.IsCanceled) { @this.moveNextTaskBuilder.SetCanceled(); }
                        else if (task.IsFaulted) { @this.moveNextTaskBuilder.SetException(Unwrap(task.Exception)); }
                        else { @this.moveNextTaskBuilder.SetResult(false); }
                        break;
                    case State.Error:
                    case State.Disposed:
                        // these states are ok to complete in because a transition to these states can happen
                        // from anywhere. In this case the task completes silently; we do not move to the completed
                        // state from either of these states
                        break;
                    default:
                        // if this runs in any other state, then there is a bug. Most likely a yield call was not awaited
                        throw ConcurrentAccessViolation();
                }
            }
        };

        private static Exception Unwrap(AggregateException aggregateException)
        {
            return aggregateException.InnerExceptions.Count == 1
                ? aggregateException.InnerException
                : aggregateException;
        }

        private Task<bool> RunYieldContinuationNoLock()
        {
            var yieldContinuation = this.yieldContinuation;
            this.yieldContinuation = null; // makes sure we never run it again
            this.state = State.WaitingForSyncYieldOrCompletion;
            try
            {
                if (yieldContinuation == null)
                {
                    // this could happen if someone doesn't immediatly await a yieldasync call
                    throw ConcurrentAccessViolation();
                }
                yieldContinuation();
            }
            catch (Exception ex) { return this.SetErrorStateNoLock(ex); }

            // at this point either a sync yield happened or we need to handle an
            // async yield. Return null so that we go around again and process based
            // on the now-current state
            return null;
        }

        private Task<bool> SetErrorStateNoLock(Exception exception)
        {
            this.state = State.Error;
            this.moveNextTaskBuilder = new TaskCompletionSource<bool>();
            this.moveNextTaskBuilder.SetException(exception);
            return this.moveNextTaskBuilder.Task;
        }

        void IDisposable.Dispose()
        {
            lock (this.@lock)
            {
                if (this.state != State.Disposed)
                {
                    this.state = State.Disposed;
                    this.asyncIteratorFunc = null;
                    this.moveNextTaskBuilder?.TrySetException(this.Disposed());
                    this.moveNextTaskBuilder = null;
                    this.currentEnumerator?.Dispose();
                    this.currentEnumerator = null;
                    this.currentValue = default(T);
                    // TODO yield continuation
                }
            }
        }
        #endregion

        // todo could move to abstract base class
        #region ---- Awaiter ----
        public struct YieldAwaitable
        {
            private readonly AsyncIteratorBuilder<T> builder;

            internal YieldAwaitable(AsyncIteratorBuilder<T> builder)
            {
                this.builder = builder;
            }

            public YieldAwaiter GetAwaiter() => new YieldAwaiter(this.builder);
        }

        public struct YieldAwaiter
        {
            private readonly AsyncIteratorBuilder<T> builder;

            internal YieldAwaiter(AsyncIteratorBuilder<T> builder)
            {
                this.builder = builder;
            }
        }
        #endregion

        #region ---- Helpers ----
        private Exception Disposed() => new ObjectDisposedException(this.GetType().ToString());

        private static Exception ConcurrentAccessViolation() => new InvalidOperationException("TODO");

        private Exception UnexpectedState() => new InvalidOperationException($"Unexpected state '{this.state}'"); 
        #endregion

        #region ---- State ----
        private enum State
        {
            NotStarted,
            WaitingForSyncYieldOrCompletion,
            WaitingForAsyncYieldOrCompletion,
            Idle,
            IdleWithEnumerator,
            Completed,
            Error,
            Disposed,
        }
        #endregion
    }
}
