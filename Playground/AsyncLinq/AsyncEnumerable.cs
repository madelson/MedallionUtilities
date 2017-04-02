using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;

namespace Playground.AsyncLinq
{
    public static class AsyncEnumerable
    {
        private static readonly Task<bool> CachedTrueTask = BooleanTask(true), CachedFalseTask = BooleanTask(false);
        private static Task<bool> _cachedCanceledBooleanTask;

        internal static Task<bool> TrueTask => CachedTrueTask;
        internal static Task<bool> FalseTask => CachedFalseTask;
        internal static Task<bool> CanceledTask
        {
            get
            {
                if (_cachedCanceledBooleanTask == null)
                {
                    var canceledTaskBuilder = new TaskCompletionSource<bool>();
                    canceledTaskBuilder.TrySetCanceled();
                    _cachedCanceledBooleanTask = canceledTaskBuilder.Task;
                }
                return _cachedCanceledBooleanTask;
            }
        }

        private static async Task<bool> BooleanTask(bool value) => value;

        public static IAsyncEnumerable<T> Create<T>(Func<AsyncIteratorBuilder<T>, Task> yieldBlock)
        {
            if (yieldBlock == null) { throw new ArgumentNullException(nameof(yieldBlock)); }

            throw new NotImplementedException();
            //return new AsyncIteratorBuilder<T>(yieldBlock);
        }

        public static IAsyncEnumerable<T> AsAsyncEnumerable<T>(this IEnumerable<Task<T>> source)
        {
            if (source == null) { throw new ArgumentNullException(nameof(source)); }

            return new TaskEnumerableAsyncEnumerable<T>(source);
        }

        private sealed class TaskEnumerableAsyncEnumerable<T> : IAsyncEnumerable<T>
        {
            private readonly IEnumerable<Task<T>> taskEnumerable;

            public TaskEnumerableAsyncEnumerable(IEnumerable<Task<T>> taskEnumerable) { this.taskEnumerable = taskEnumerable; }

            IAsyncEnumerator<T> IAsyncEnumerable<T>.GetEnumerator() => new Enumerator(this.taskEnumerable.GetEnumerator());

            private sealed class Enumerator : IAsyncEnumerator<T>
            {
                private IEnumerator<Task<T>> taskEnumerator;

                public Enumerator(IEnumerator<Task<T>> taskEnumerator) { this.taskEnumerator = taskEnumerator; }

                T IAsyncEnumerator<T>.Current
                {
                    get
                    {
                        if (this.taskEnumerator == null) { throw new ObjectDisposedException(nameof(Enumerator)); }

                        var currentTask = this.taskEnumerator.Current;
                        if (currentTask == null) { throw new InvalidOperationException("the underlying enumerable contained a null task"); }

                        if (!currentTask.IsCompleted) { throw new InvalidOperationException($"{nameof(IAsyncEnumerator<T>.Current)} cannot be called concurrently with {nameof(IAsyncEnumerator<T>.MoveNextAsync)}"); }

                        return currentTask.Result;
                    }
                }

                void IDisposable.Dispose()
                {
                    if (this.taskEnumerator != null)
                    {
                        this.taskEnumerator.Dispose();
                        this.taskEnumerator = null;
                    }
                }

                Task<bool> IAsyncEnumerator<T>.MoveNextAsync()
                {
                    if (this.taskEnumerator == null) { throw new ObjectDisposedException(nameof(Enumerator)); }
                    if (!this.taskEnumerator.Current.IsCompleted)
                    {
                        throw new InvalidOperationException($"Cannot make concurrent calls to {nameof(IAsyncEnumerator<T>.MoveNextAsync)}");
                    }

                    if (!this.taskEnumerator.MoveNext()) { return FalseTask; }

                    return this.taskEnumerator.Current.ThenSynchronous(_ => true);
                }
            }
        }

        public static IAsyncEnumerable<T> AsAsyncEnumerable<T>(this IEnumerable<T> source)
        {
            if (source == null) { throw new ArgumentNullException(nameof(source)); }

            return new EnumerableAsyncEnumerable<T>(source);
        }

        private sealed class EnumerableAsyncEnumerable<T> : IAsyncEnumerable<T>
        {
            private readonly IEnumerable<T> enumerable;

            public EnumerableAsyncEnumerable(IEnumerable<T> enumerable) { this.enumerable = enumerable; }

            IAsyncEnumerator<T> IAsyncEnumerable<T>.GetEnumerator() => new Enumerator(this.enumerable.GetEnumerator());

            private sealed class Enumerator : IAsyncEnumerator<T>
            {
                private readonly IEnumerator<T> enumerator;

                public Enumerator(IEnumerator<T> enumerator) { this.enumerator = enumerator; }

                T IAsyncEnumerator<T>.Current => this.enumerator.Current;

                void IDisposable.Dispose() => this.enumerator.Dispose();

                Task<bool> IAsyncEnumerator<T>.MoveNextAsync() => this.enumerator.MoveNext() ? TrueTask : FalseTask;
            }
        }

        public static IAsyncEnumerable<T> OrderByCompletion<T>(this IEnumerable<Task<T>> source)
        {
            throw new NotImplementedException("partial below");
        }

        private sealed class OrderByCompletionAsyncEnumerable<T> : IAsyncEnumerable<T>
        {
            private readonly IEnumerable<Task<T>> source;

            public OrderByCompletionAsyncEnumerable(IEnumerable<Task<T>> source)
            {
                this.source = source;
            }

            IAsyncEnumerator<T> IAsyncEnumerable<T>.GetEnumerator()
            {
                throw new NotImplementedException();
            }

            private sealed class Enumerator : IAsyncEnumerator<T>
            {
                private readonly object @lock = new object();
                private IEnumerable<Task<T>> source;
                private Queue<Task<T>> queue;
                private int pendingCount;
                private TaskCompletionSource<bool> moveNextTaskBuilder;
                private Task<T> currentTask;

                public Enumerator(IEnumerable<Task<T>> source)
                {
                    this.source = source;
                }

                T IAsyncEnumerator<T>.Current
                {
                    get
                    {
                        lock (this.@lock)
                        {
                            if (this.pendingCount < 0)
                            {
                                throw new ObjectDisposedException(this.GetType().ToString());
                            }
                            throw new NotImplementedException();
                        }
                    }
                }

                void IDisposable.Dispose()
                {
                    this.source = null; // prevent initialization
                    var queue = this.queue;
                    if (this.queue != null)
                    {
                        lock (this.queue)
                        {
                            this.queue.Clear();
                            this.pendingCount = -1;
                            this.moveNextTaskBuilder?.SetException(new ObjectDisposedException(this.GetType().ToString()));
                            this.moveNextTaskBuilder = null;
                            this.currentTask = null;
                        }
                    }
                }

                Task<bool> IAsyncEnumerator<T>.MoveNextAsync()
                {
                    if (this.source != null)
                    {
                        this.Initialize();
                    }

                    lock (this.queue)
                    {
                        if (this.pendingCount < 0)
                        {
                            throw new ObjectDisposedException(this.GetType().ToString());
                        }

                        // case 1: we have something queued up
                        if (this.queue.Count > 0)
                        {
                            this.currentTask = this.queue.Dequeue();
                            if (this.currentTask.IsCanceled)
                            {
                                return CanceledTask;
                            }
                            if (this.currentTask.IsFaulted)
                            {
                                var exception = this.currentTask.Exception.InnerExceptions.Count == 1
                                    ? this.currentTask.Exception.InnerException
                                    : this.currentTask.Exception;
                                return Task.FromException<bool>(exception);
                            }
                            return TrueTask;
                        }
                        
                        this.currentTask = null; // helps catch errors

                        // case 2: nothing queued up but there are still tasks pending
                        if (this.pendingCount > 0)
                        {
                            this.moveNextTaskBuilder = new TaskCompletionSource<bool>();
                            return this.moveNextTaskBuilder.Task;
                        }

                        // case 3: we're done
                        return FalseTask;
                    }
                }

                private void Initialize()
                {
                    var queue = new Queue<Task<T>>();
                    lock (queue) // because tasks can start completing as we're processing
                    {
                        foreach (var task in this.source)
                        {
                            if (task == null) { throw new ArgumentNullException(nameof(source), $"the {nameof(source)} argument passed to {nameof(OrderByCompletion)} must not contain any null tasks"); }

                            if (task.IsCompleted)
                            {
                                queue.Enqueue(task);
                            }
                            else
                            {
                                ++this.pendingCount;
                                task.ContinueWith(PlaceCompletedTaskInQueue, state: this, continuationOptions: TaskContinuationOptions.ExecuteSynchronously);
                            }
                        }
                    }

                    this.source = null;
                }

                private static void PlaceCompletedTaskInQueue(Task<T> task, object state)
                {
                    var enumerator = (Enumerator)state;
                    lock (enumerator.queue)
                    {
                        --enumerator.pendingCount;

                        // in the case that the completion of this task is what causes an outstanding MoveNextAsync
                        // task to finish, we have to complete that task
                        if (enumerator.queue.Count == 0 && enumerator.moveNextTaskBuilder?.Task.IsCompleted == false)
                        {
                            enumerator.currentTask = task;
                            if (task.IsCanceled)
                            {
                                enumerator.moveNextTaskBuilder.SetCanceled();
                            }
                            else if (task.IsFaulted)
                            {
                                var exception = task.Exception.InnerExceptions.Count == 1
                                    ? task.Exception.InnerException
                                    : task.Exception;
                                enumerator.moveNextTaskBuilder.SetException(exception);
                            }
                            else
                            {
                                enumerator.moveNextTaskBuilder.SetResult(true);
                            }
                        }
                        else
                        {
                            enumerator.queue.Enqueue(task);
                        }
                    }
                }
            }
        }

        public static Task ForEachAsync<T>(this IAsyncEnumerable<T> source, Action<T> action)
        {
            if (source == null) { throw new ArgumentNullException(nameof(source)); }
            if (action == null) { throw new ArgumentNullException(nameof(action)); }

            return InternalForEachAsync(source, action);
        }

        private static async Task InternalForEachAsync<T>(IAsyncEnumerable<T> source, Action<T> action)
        {
            using (var enumerator = source.GetEnumerator())
            {
                while (await enumerator.MoveNextAsync().ConfigureAwait(false)) { action(enumerator.Current); }
            }
        }

        public static Task ForEachAsync<T>(this IAsyncEnumerable<T> source, Func<T, Task> asyncAction)
        {
            if (source == null) { throw new ArgumentNullException(nameof(source)); }
            if (asyncAction == null) { throw new ArgumentNullException(nameof(asyncAction)); }

            return InternalForEachAsync(source, asyncAction);
        }

        private static async Task InternalForEachAsync<T>(IAsyncEnumerable<T> source, Func<T, Task> asyncAction)
        {
            using (var enumerator = source.GetEnumerator())
            {
                while (await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    await asyncAction(enumerator.Current).ConfigureAwait(false);
                }
            }
        }
        
        public static Task<TAccumulate> AggregateAsync<TSource, TAccumulate>(this IAsyncEnumerable<TSource> source, TAccumulate seed, Func<TAccumulate, TSource, TAccumulate> func)
        {
            if (source == null) { throw new ArgumentNullException(nameof(source)); }
            if (func == null) { throw new ArgumentNullException(nameof(func)); }

            return InternalAggregateAsync(source, seed, func);
        }

        private static async Task<TAccumulate> InternalAggregateAsync<TSource, TAccumulate>(IAsyncEnumerable<TSource> source, TAccumulate seed, Func<TAccumulate, TSource, TAccumulate> func)
        {
            var accumulator = seed;
            using (var enumerator = source.GetEnumerator())
            {
                while (await enumerator.MoveNextAsync().ConfigureAwait(false)) { accumulator = func(accumulator, enumerator.Current); }
            }

            return accumulator;
        }

        public static Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source)
        {
            if (source == null) { throw new ArgumentNullException(nameof(source)); }

            return InternalAggregateAsync(source, new List<T>(), (list, element) => { list.Add(element); return list; });
        }

        //public static IEnumerable<Task<T>> AsTaskEnumerable<T>(this IAsyncEnumerable<T> source)
        //{
        //    if (source == null) { throw new ArgumentNullException(nameof(source)); }

        //    var existingTaskEnumerable = source as IEnumerable<Task<T>>;
        //    if (existingTaskEnumerable != null) { return existingTaskEnumerable; }

        //    return TaskIterator(source);
        //}

        //private static IEnumerable<Task<T>> TaskIterator<T>(IAsyncEnumerable<T> source)
        //{
        //    using (var enumerator = source.GetEnumerator())
        //    {
        //        var lastMoveNextAsync = enumerator.MoveNextAsync();
        //        while (true)
        //        {
        //            if (lastMoveNextAsync.IsCompleted)
        //            {
        //                if ()
        //            }
        //        }
        //    }
        //}

        public static IAsyncEnumerable<TResult> Select<TSource, TResult>(this IAsyncEnumerable<TSource> source, Func<TSource, TResult> selector)
        {
            if (source == null) { throw new ArgumentNullException(nameof(source)); }
            if (selector == null) { throw new ArgumentNullException(nameof(selector)); }

            return Create<TResult>(async builder =>
            {
                using (var enumerator = source.GetEnumerator())
                {
                    while (await enumerator.MoveNextAsync().ConfigureAwait(false))
                    {
                        await builder.YieldAsync(selector(enumerator.Current));
                    }
                }
            });
        }

        public static IAsyncEnumerable<TResult> Select<TSource, TResult>(this IAsyncEnumerable<TSource> source, Func<TSource, Task<TResult>> selector)
        {
            if (source == null) { throw new ArgumentNullException(nameof(source)); }
            if (selector == null) { throw new ArgumentNullException(nameof(selector)); }

            return Create<TResult>(async builder =>
            {
                using (var enumerator = source.GetEnumerator())
                {
                    while (await enumerator.MoveNextAsync().ConfigureAwait(false))
                    {
                        var result = await selector(enumerator.Current).ConfigureAwait(false);
                        await builder.YieldAsync(result);
                    }
                }
            });
        }

        public static IAsyncEnumerable<TSource> Where<TSource>(this IAsyncEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            if (source == null) { throw new ArgumentNullException(nameof(source)); }
            if (predicate == null) { throw new ArgumentNullException(nameof(predicate)); }

            return Create<TSource>(async builder =>
            {
                using (var enumerator = source.GetEnumerator())
                {
                    while (await enumerator.MoveNextAsync().ConfigureAwait(false))
                    {
                        var current = enumerator.Current;
                        if (predicate(current)) { await builder.YieldAsync(current); }
                    }
                }
            });
        }

        public static IAsyncEnumerable<TSource> Where<TSource>(this IAsyncEnumerable<TSource> source, Func<TSource, Task<bool>> asyncPredicate)
        {
            if (source == null) { throw new ArgumentNullException(nameof(source)); }
            if (asyncPredicate == null) { throw new ArgumentNullException(nameof(asyncPredicate)); }

            return Create<TSource>(async builder =>
            {
                using (var enumerator = source.GetEnumerator())
                {
                    while (await enumerator.MoveNextAsync().ConfigureAwait(false))
                    {
                        var current = enumerator.Current;
                        if (await asyncPredicate(current).ConfigureAwait(false)) { await builder.YieldAsync(current); }
                    }
                }
            });
        }

        public static IAsyncEnumerable<TResult> Zip<TFirst, TSecond, TResult>(
            this IAsyncEnumerable<TFirst> first, 
            IAsyncEnumerable<TSecond> second, 
            Func<TFirst, TSecond, TResult> resultSelector)
        {
            if (first == null) { throw new ArgumentNullException(nameof(first)); }
            if (second == null) { throw new ArgumentNullException(nameof(second)); }
            if (resultSelector == null) { throw new ArgumentNullException(nameof(resultSelector)); }

            return Create<TResult>(async builder =>
            {
                using (var firstEnumerator = first.GetEnumerator())
                using (var secondEnumerator = second.GetEnumerator())
                {
                    while (await firstEnumerator.MoveNextAsync().ConfigureAwait(false)
                        && await secondEnumerator.MoveNextAsync().ConfigureAwait(false))
                    {
                        await builder.YieldAsync(resultSelector(firstEnumerator.Current, secondEnumerator.Current));
                    }
                }
            });
        }

        public static IAsyncEnumerable<TResult> Join<TOuter, TInner, TKey, TResult>(
            IAsyncEnumerable<TOuter> outer,
            IAsyncEnumerable<TInner> inner,
            Func<TOuter, TKey> outerKeySelector,
            Func<TInner, TKey> innerKeySelector,
            Func<TOuter, TInner, TResult> resultSelector,
            IEqualityComparer<TKey> comparer = null)
        {
            if (outer == null) { throw new ArgumentNullException(nameof(outer)); }
            if (inner == null) { throw new ArgumentNullException(nameof(inner)); }
            if (outerKeySelector == null) { throw new ArgumentNullException(nameof(outerKeySelector)); }
            if (innerKeySelector == null) { throw new ArgumentNullException(nameof(innerKeySelector)); }
            if (resultSelector == null) { throw new ArgumentNullException(nameof(resultSelector)); }

            return Create<TResult>(async builder =>
            {
                ILookup<TKey, TInner> lookup = null; // todo

                using (var outerEnumerator = outer.GetEnumerator())
                {
                    while (await outerEnumerator.MoveNextAsync().ConfigureAwait(false))
                    {
                        var outerElement = outerEnumerator.Current;
                        var grouping = lookup[outerKeySelector(outerElement)];
                        foreach (var innerElement in grouping)
                        {
                            await builder.YieldAsync(resultSelector(outerElement, innerElement));
                        }
                    }
                }
            });
        }

        private static Task<TResult> ThenSynchronous<TTask, TResult>(this TTask task, Func<TTask, TResult> selector)
            where TTask : Task
        {
            return task.ContinueWith(
                continuationFunction: ThenSynchronousContinuation<TTask, TResult>.Instance,
                state: selector,
                continuationOptions: TaskContinuationOptions.ExecuteSynchronously
            );
        }

        private static class ThenSynchronousContinuation<TTask, TResult>
            where TTask : Task
        {
            public static readonly Func<Task, object, TResult> Instance = (task, state) =>
            {
                if (task.IsCanceled) { throw new TaskCanceledException(); }
                if (task.IsFaulted)
                {
                    var exception = task.Exception.InnerExceptions.Count == 1
                        ? task.Exception.InnerException
                        : task.Exception;
                    ExceptionDispatchInfo.Capture(exception).Throw();
                }

                return ((Func<TTask, TResult>)state)((TTask)task);
            };
        }
    }

    public interface IAsyncEnumerable<out T>
    {
        IAsyncEnumerator<T> GetEnumerator();
    }

    public interface IAsyncEnumerator<out T> : IDisposable
    {
        T Current { get; }
        Task<bool> MoveNextAsync();
    }
}
