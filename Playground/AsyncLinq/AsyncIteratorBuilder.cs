using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Playground.AsyncLinq
{
    public sealed class AsyncIteratorBuilder<T> : IAsyncEnumerator<T>
    {
        private readonly object @lock = new object();

        private State state = State.NotStarted;
        private Func<AsyncIteratorBuilder<T>, Task> asyncIteratorFunc;
        private readonly bool yieldThrowsOnDispose;
        private TaskCompletionSource<bool> moveNextTaskBuilder;
        private T currentValue;
        private IEnumerator<T> currentEnumerator;
        private Action yieldContinuation;
        private Exception yieldedEnumeratorOrCompletionException;

        internal AsyncIteratorBuilder(
            Func<AsyncIteratorBuilder<T>, Task> asyncIteratorFunc,
            bool yieldThrowsOnDispose)
        {
            if (asyncIteratorFunc == null) { throw new ArgumentNullException(nameof(asyncIteratorFunc)); }

            this.asyncIteratorFunc = asyncIteratorFunc;
            this.yieldThrowsOnDispose = yieldThrowsOnDispose;
        }

        #region ---- Builder API ----
        public YieldAwaitable YieldAsync(T value)
        {
            this.Yield(value, nextEnumerator: null);
            return new YieldAwaitable(this, isCompleted: false);
        }

        public YieldAwaitable YieldEachAsync(IEnumerable<T> values)
        {
            if (values == null) { throw new ArgumentNullException(nameof(values)); }

            var enumerator = values.GetEnumerator();
            bool hasValue;
            try { hasValue = enumerator.MoveNext(); }
            catch
            {
                enumerator.Dispose();
                throw;
            }
            
            if (!hasValue)
            {
                enumerator.Dispose();
                // if there are no values, just return a completed awaiter so that
                // the async iterator can continue synchronously
                return new YieldAwaitable(this, isCompleted: true);
            }

            this.Yield(enumerator.Current, enumerator);
            return new YieldAwaitable(this, isCompleted: false);
        }

        private void Yield(T nextValue, IEnumerator<T> nextEnumerator)
        {
            lock (this.@lock)
            {
                switch (this.state)
                {
                    case State.WaitingForSyncYieldOrCompletion:
                    case State.WaitingForAsyncYieldOrCompletion:
                        this.state = State.Yielding;
                        this.currentEnumerator = nextEnumerator;
                        this.currentValue = nextValue;
                        break;
                    case State.Yielding:
                    case State.Idle:
                    case State.Completed:
                        // these states indicate a failure to await a yield. We don't need to go into the
                        // error state here since nothing is corrupted
                        throw ConcurrentAccessViolation();
                    case State.Error:
                        throw Rethrow(this.moveNextTaskBuilder.Task.Exception);
                    case State.Disposed:
                        // todo this is wrong
                        // this can happen if the iterator is disposed while still running. Just throw
                        throw this.Disposed();
                    default:
                        throw this.UnexpectedState(); 
                }
            }
        }

        public Task YieldEachAsync(IAsyncEnumerable<T> values)
        {
            if (values == null) { throw new ArgumentNullException(nameof(values)); }

            return this.InternalYieldEachAsync(values);
        }

        private async Task InternalYieldEachAsync(IAsyncEnumerable<T> values)
        {
            using (var enumerator = values.GetEnumerator())
            {
                // todo ConfigureAwait()
                while (await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    if (!await this.YieldAsync(enumerator.Current)) { break; }
                }
            }
        }
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
                        case State.Yielding:
                            throw ConcurrentAccessViolation();
                        case State.Idle:
                            return this.currentValue;
                        case State.Completed:
                            throw new InvalidOperationException("The enumeration has already finished");
                        case State.Error:
                            throw Rethrow(this.moveNextTaskBuilder.Task.Exception);
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
                    
                    // have the iterator terminate when the task finishes
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
                case State.Yielding:
                    // this means that someone called MoveNextAsync() before the previous MoveNextAsync call completed
                    throw ConcurrentAccessViolation();
                case State.Idle:
                    if (this.currentEnumerator != null)
                    {
                        // this means that we potentially have more values queued up in a synchronous enumerator.
                        // We'll try to pull one from there if we can
                        try
                        {
                            if (this.currentEnumerator.MoveNext())
                            {
                                this.currentValue = this.currentEnumerator.Current;
                                return AsyncEnumerable.TrueTask; // complete synchronously
                            }

                            // enumerator is exhausted, so just clear it
                            this.currentEnumerator.Dispose();
                            this.currentEnumerator = null;
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
                    }

                    // this means that we're not waiting for anything. We should have a yield continuation 
                    // though, so invoke that
                    return this.RunYieldContinuationNoLock();
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
                            : task.IsFaulted ? task.Exception.GetBaseException()
                            : null;
                        break;
                    case State.WaitingForAsyncYieldOrCompletion:
                        // this means that the async iterator func task completed asynchronously and thus needs to finish
                        // off the MoveNextAsync task. Do that, propagating cancellation/exceptions as necessary
                        @this.state = State.Completed;
                        // note: setting the builder must go after setting the state in case someone has put a sync continuation on it!
                        if (task.IsCanceled) { @this.moveNextTaskBuilder.SetCanceled(); }
                        else if (task.IsFaulted) { @this.moveNextTaskBuilder.SetException(task.Exception.GetBaseException()); }
                        else { @this.moveNextTaskBuilder.SetResult(false); }
                        break;
                    case State.Error:
                    case State.Disposed:
                        // these states are ok to complete in because a transition to these states can happen
                        // from anywhere. In this case the task completes silently; we do not move to the completed
                        // state from either of these states
                        break;
                    case State.Yielding:
                    case State.Idle:
                        // in these states, there is a bug in the calling code. Most likely a yield call was not awaited
                        throw ConcurrentAccessViolation();
                    default:
                        // in any other state, it's a bug in our code
                        throw @this.UnexpectedState();
                }
            }
        };

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
                    // note: setting the builder must go AFTER setting the state in case of a sync continuation
                    this.moveNextTaskBuilder?.TrySetException(this.Disposed());
                    this.moveNextTaskBuilder = null;
                    this.currentEnumerator?.Dispose();
                    this.currentEnumerator = null;
                    this.currentValue = default(T);
                    this.yieldedEnumeratorOrCompletionException = null;
                    var yieldContinuation = this.yieldContinuation;
                    if (yieldContinuation != null)
                    {
                        this.yieldContinuation = null;
                        // if we dispose the enumerator with a lingering continuation, we need to invoke it
                        // to allow for cleanup. Do this in a task in case it takes a long time
                        Task.Run(yieldContinuation);
                    }
                }
            }
        }
        #endregion

        #region ---- Awaiter ----
        public struct YieldAwaitable
        {
            private readonly AsyncIteratorBuilder<T> builder;
            private readonly bool isCompleted;

            internal YieldAwaitable(AsyncIteratorBuilder<T> builder, bool isCompleted)
            {
                this.builder = builder;
                this.isCompleted = isCompleted;
            }

            public YieldAwaiter GetAwaiter() => new YieldAwaiter(this.builder, this.isCompleted);
        }

        public struct YieldAwaiter : ICriticalNotifyCompletion
        {
            private readonly AsyncIteratorBuilder<T> builder;
            
            internal YieldAwaiter(AsyncIteratorBuilder<T> builder, bool isCompleted)
            {
                this.builder = builder;
                this.IsCompleted = isCompleted;
            }

            public bool IsCompleted { get; }

            public bool GetResult()
            {
                lock (this.builder.@lock)
                {
                    if (this.builder.state == State.Disposed)
                    {
                        if (this.builder.yieldThrowsOnDispose) { throw this.builder.Disposed(); }
                        return false;
                    }
                    return true;
                }
            }

            public void OnCompleted(Action continuation)
            {
                if (continuation == null) { throw new ArgumentNullException(nameof(continuation)); }

                // see https://blogs.msdn.microsoft.com/pfxteam/2012/02/29/whats-new-for-parallelism-in-net-4-5-beta/
                // for why OnCompleted must explicitly flow the EC
                var executionContext = ExecutionContext.Capture();
                this.InternalOnCompleted(() => ExecutionContext.Run(executionContext, state => ((Action)state)(), state: continuation));
            }

            public void UnsafeOnCompleted(Action continuation)
            {
                if (continuation == null) { throw new ArgumentNullException(nameof(continuation)); }

                this.InternalOnCompleted(continuation);
            }

            private void InternalOnCompleted(Action continuation)
            {
                lock (this.builder.@lock)
                {
                    switch (this.builder.state)
                    {
                        case State.Yielding:
                            this.builder.state = State.Idle;
                            this.builder.yieldContinuation = continuation;
                            // trigger the builder if we have one (only in the async case)
                            var moveNextTaskBuilder = this.builder.moveNextTaskBuilder;
                            if (moveNextTaskBuilder != null)
                            {
                                this.builder.moveNextTaskBuilder = null;
                                moveNextTaskBuilder.SetResult(true);
                            }
                            return; // all done
                        case State.Idle:
                        case State.WaitingForAsyncYieldOrCompletion:
                        case State.WaitingForSyncYieldOrCompletion:
                        case State.Completed:
                            this.builder.SetErrorStateNoLock(ConcurrentAccessViolation());
                            break;
                        case State.Disposed:
                        case State.Error:
                            // just break, since we'll let GetResult deal with it
                            break;
                        default:
                            this.builder.SetErrorStateNoLock(this.builder.UnexpectedState());
                            break;
                    }
                }

                // if we reach here, we invoke the continuation immediately. The reason is because
                // in an error/disposed state we need to allow the task to finish so that it can run
                // any finally blocks or other cleanup logic
                continuation();
            }
        }
        #endregion

        #region ---- Helpers ----
        private Exception Disposed() => new ObjectDisposedException(this.GetType().ToString());

        private static Exception ConcurrentAccessViolation() => new InvalidOperationException(
            "The current operation is not valid given the current state of the iterator. Make sure that: "
            + $"(a) each {nameof(IAsyncEnumerator<T>.MoveNextAsync)} is awaited before the next call begins, "
            + $"(b) each {nameof(YieldAsync)}/{nameof(YieldEachAsync)} call is immediately awaited, "
            + $"and (c) the {nameof(IAsyncEnumerator<T>.Current)} property is only used after awaiting a call to {nameof(IAsyncEnumerator<T>.MoveNextAsync)}"
        );

        private Exception UnexpectedState() => new InvalidOperationException($"Unexpected state '{this.state}'");

        private static Exception Rethrow(AggregateException aggregateException)
        {
            ExceptionDispatchInfo.Capture(aggregateException.GetBaseException()).Throw();
            throw new InvalidOperationException("will never get here");
        }
        #endregion

        #region ---- State ----
        private enum State
        {
            /// <summary>
            /// The initial state. <see cref="asyncIteratorFunc"/> has not been invoked
            /// </summary>
            NotStarted,
            /// <summary>
            /// <see cref="IAsyncEnumerator{T}.MoveNextAsync"/> has been called, and we are looking to see
            /// if <see cref="asyncIteratorFunc"/> or <see cref="yieldContinuation"/> will call <see cref="YieldAsync(T)"/>
            /// synchronously
            /// </summary>
            WaitingForSyncYieldOrCompletion,
            /// <summary>
            /// <see cref="IAsyncEnumerator{T}.MoveNextAsync"/> has been called, and we waiting for
            /// <see cref="asyncIteratorFunc"/> or <see cref="yieldContinuation"/> to call <see cref="YieldAsync(T)"/>
            /// asynchronously
            /// </summary>
            WaitingForAsyncYieldOrCompletion,
            /// <summary>
            /// Transitional state between when <see cref="YieldAsync(T)"/> is called and the returned awaiter's
            /// <see cref="YieldAwaiter.UnsafeOnCompleted(Action)"/> is called
            /// </summary>
            Yielding,
            /// <summary>
            /// The iterator is in a balanced (stopped) state, with no pending work. This is the state in which
            /// <see cref="IAsyncEnumerator{T}.Current"/> should be used
            /// </summary>
            Idle,
            /// <summary>
            /// The iterator has finished (<see cref="asyncIteratorFunc"/>'s task has terminated)
            /// </summary>
            Completed,
            /// <summary>
            /// A terminal state that indicates a critical error, such as <see cref="asyncIteratorFunc"/> or
            /// a <see cref="yieldContinuation"/> throwing an exception
            /// </summary>
            Error,
            /// <summary>
            /// <see cref="IDisposable.Dispose"/> has been called
            /// </summary>
            Disposed,
        }
        #endregion
    }
}
