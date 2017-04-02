//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Runtime.CompilerServices;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;

//namespace Playground.AsyncLinq
//{
//    // note: calling the generator func or the yield continuation inside the lock
//    // is safe, since either they will call back on the same thread (via yield) or
//    // will call back on another thread, in which case they will block while we set
//    // up the TCS

//    // note: we likely want to continue on the asyncIterator task to have it close any
//    // open TCS. This will catch finishing but also thrown exceptions in the iterator block
//    // This should use TrySet

//    public sealed class AsyncIteratorBuilder<T>
//    {
//        private readonly object @lock = new object();
//        private readonly Func<AsyncIteratorBuilder<T>, Task> asyncIterator;

//        private State state;
//        private Task asyncIteratorTask;

//        private T next;
//        private TaskCompletionSource<bool> moveNextAsyncTaskBuilder;
//        private Action yieldContinuation;

//        internal AsyncIteratorBuilder(Func<AsyncIteratorBuilder<T>, Task> asyncIterator)
//        {
//            this.asyncIterator = asyncIterator;
//        }

//        internal Task<bool> MoveNextAsync()
//        {
//            lock (this.@lock)
//            {
//                while (true) // loop until we reach a "stable" state
//                {
//                    switch (this.state)
//                    {
//                        case State.NotStarted:
//                            this.state = State.WaitingForYield;
//                            try { this.asyncIteratorTask = this.asyncIterator(this); }
//                            catch
//                            {
//                                this.state = State.Finished;
//                                throw;
//                            }
//                            break;

//                        case State.WaitingForYield:
//                            // first check for the entire iterator having completed
//                            if (this.asyncIteratorTask.IsCompleted)
//                            {
//                                this.state = State.Finished;

//                                // if it failed, return the exception
//                                if (this.asyncIteratorTask.Exception != null)
//                                {
//                                    var faultedTaskBuilder = new TaskCompletionSource<bool>();
//                                    faultedTaskBuilder.SetException(this.asyncIteratorTask.Exception.InnerExceptions);
//                                    return faultedTaskBuilder.Task;
//                                }

//                                // otherwise return no more elements
//                                return AsyncEnumerable.FalseTask;
//                            }

//                            // next check if we have a yield continuation to execute and
//                            // execute it if so
//                            var yieldContinuation = this.yieldContinuation;
//                            if (yieldContinuation != null)
//                            {
//                                this.yieldContinuation = null;
//                                try { this.yieldContinuation(); }
//                                catch
//                                {
//                                    this.state = State.Finished;
//                                    throw;
//                                }

//                                // go around again: maybe the state has changed
//                                break;
//                            }

//                            // finally, return a promise that will be completed when a value does come in
//                            return (this.moveNextAsyncTaskBuilder = new TaskCompletionSource<bool>()).Task;

//                        case State.Idle:
//                            this.state = State.WaitingForYield;
//                            break;

//                        case State.WaitingForMoveNext:
//                            return AsyncEnumerable.TrueTask;

//                        case State.Finished:
//                            return AsyncEnumerable.FalseTask;
//                        case State.Disposed:
//                            throw new ObjectDisposedException("AsyncEnumerator");
//                    }
//                }
//            }
//        }
        
//        internal T ConsumeNextValue()
//        {
//            lock (this.@lock)
//            {
//                if (this.state != State.WaitingForConsumeNextValue)
//                {
//                    throw new InvalidOperationException($"{nameof(ConsumeNextValue)} called from invalid state {this.state}");
//                }

//                this.state = State.Idle;
//                return this.next;
//            }
//        }

//        public YieldAwaitable YieldAsync(T value)
//        {
//            lock (this.@lock)
//            {
//                switch (this.state)
//                {
//                    case State.WaitingForYield:
//                        this.next = value;
//                        this.state = State.WaitingForMoveNext;
//                        return new YieldAwaitable(this, completedSynchronously: true);
//                    case State.Idle:
//                        this.next = value;
//                        this.state = State.WaitingForMoveNext;
//                        return new YieldAwaitable(this, completedSynchronously: false);
//                    default:
//                        throw new InvalidOperationException("the iterator is not in a valid state for yielding");
//                }
//            }
//        }

//        private void AddYieldContinuation(Action yieldContinuation)
//        {
//            bool executeSynchronously;
//            lock (this.@lock)
//            {
//                if (this.yieldContinuation != null) { throw new InvalidOperationException("cannot yield concurrently"); }
                
//                switch (this.state)
//                {
//                    case State.WaitingForYield:
//                        executeSynchronously = true;
//                        break;
//                    case State.WaitingForMoveNext:
//                        this.yieldContinuation = yieldContinuation;
//                        executeSynchronously = false;
//                        break;
//                    default:
//                        throw new InvalidOperationException("invalid state");
//                }
//            }

//            if (executeSynchronously) { yieldContinuation(); }
//        }

//        private bool GetAwaiterResult()
//        {
//            throw new NotImplementedException();
//        }

//        private enum State
//        {
//            /// <summary>
//            /// <see cref="asyncIterator"/> has not yet been invoked
//            /// </summary>
//            NotStarted = 0,
//            /// <summary>
//            /// A value has been yielded for each call to <see cref="MoveNextAsync"/>
//            /// </summary>
//            Idle = 1,
//            /// <summary>
//            /// <see cref="MoveNextAsync"/> has been called, but we haven't yielded a value yet
//            /// </summary>
//            WaitingForYield = 2,
//            /// <summary>
//            /// A value has been yielded but has not been consumed by <see cref="MoveNextAsync"/>
//            /// </summary>
//            WaitingForMoveNext = 3,
//            /// <summary>
//            /// A value has been yielded and associated with a call to <see cref="MoveNextAsync"/>, but
//            /// has not been consumed
//            /// </summary>
//            WaitingForConsumeNextValue = 4,
//            /// <summary>
//            /// All values have been yielded
//            /// </summary>
//            Finished = 4,
//            /// <summary>
//            /// The iterator has been disposed
//            /// </summary>
//            Disposed = 5,
//        }

//        public struct YieldAwaitable
//        {
//            private AsyncIteratorBuilder<T> builder;
//            private bool completedSynchronously;

//            internal YieldAwaitable(AsyncIteratorBuilder<T> builder, bool completedSynchronously)
//            {
//                this.builder = builder;
//                this.completedSynchronously = completedSynchronously;
//            }

//            public YieldAwaiter GetAwaiter()
//            {
//                if (this.builder == null) { throw new InvalidOperationException($"an awaitable must be produced by a call to {nameof(AsyncIteratorBuilder<T>.YieldAsync)} and may only be awaited once"); }

//                var awaiter = new YieldAwaiter(this.builder, this.completedSynchronously);
//                this.builder = null;
//                return awaiter;
//            }
//        }

//        public struct YieldAwaiter : ICriticalNotifyCompletion
//        {
//            private AsyncIteratorBuilder<T> builder;
            
//            internal YieldAwaiter(AsyncIteratorBuilder<T> builder)
//            {
//                this.builder = builder;
//            }

//            // NOTE: this always returns false because we never want to just keep going after yielding
//            // a value. Instead, we'll always stop execution until MoveNextAsync() is called again
//            public bool IsCompleted => false;

//            public bool GetResult() => this.builder.GetAwaiterResult();

//            public void OnCompleted(Action continuation)
//            {
//                if (continuation == null) { throw new ArgumentNullException(nameof(continuation)); }

//                // see https://blogs.msdn.microsoft.com/pfxteam/2012/02/29/whats-new-for-parallelism-in-net-4-5-beta/
//                // for why OnCompleted must explicitly flow the EC
//                var executionContext = ExecutionContext.Capture();
//                this.builder.AddYieldContinuation(() => ExecutionContext.Run(executionContext, state => ((Action)state)(), state: continuation));
//            }

//            public void UnsafeOnCompleted(Action continuation)
//            {
//                if (continuation == null) { throw new ArgumentNullException(nameof(continuation)); }

//                this.builder.AddYieldContinuation(continuation);
//            }
//        }
//    }
//}
