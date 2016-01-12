using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.Collections
{
    /// <summary>
    /// Provides utilities for iterating over DAG structures
    /// </summary>
    public static class Traverse
    {
        #region ---- Along ----
        /// <summary>
        /// Enumerates the implicit sequence starting from <paramref name="root"/>
        /// and following the chain of <paramref name="next"/> calls until a null value
        /// is encountered. For example, this can be used to traverse a chain of exceptions:
        /// <code>
        ///     var innermostException = Traverse.Along(exception, e => e.InnerException).Last();
        /// </code>
        /// </summary>
        public static IEnumerable<T> Along<T>(T root, Func<T, T> next)
            where T : class
        {
            if (next == null) { throw new ArgumentNullException(nameof(next)); }

            return AlongIterator(root, next);
        }

        private static IEnumerable<T> AlongIterator<T>(T root, Func<T, T> next)
        {
            for (var node = root; node != null; node = next(node))
            {
                yield return node;
            }
        }
        #endregion

        #region ---- Breadth-First ----
        /// <summary>
        /// Enumerates the implicit tree described by <paramref name="root"/> and <paramref name="children"/>
        /// in a breadth-first manner. For example, this could be used to enumerate the exceptions of an
        /// <see cref="AggregateException"/>:
        /// <code>
        ///     var allExceptions = Traverse.BreadthFirst((Exception)new AggregateException(), e => (e as AggregateException)?.InnerExceptions ?? Enumerable.Empty<Exception>());
        /// </code>
        /// </summary>
        public static IEnumerable<T> BreadthFirst<T>(T root, Func<T, IEnumerable<T>> children)
        {
            if (children == null) { throw new ArgumentNullException(nameof(children)); }

            return BreadthFirstIterator(root, children);
        }

        private static IEnumerable<T> BreadthFirstIterator<T>(T root, Func<T, IEnumerable<T>> children)
        {
            var queue = new Queue<T>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                var next = queue.Dequeue();
                yield return next;

                foreach (var child in children(next))
                {
                    queue.Enqueue(child);
                }
            }
        }
        #endregion

        #region ---- Depth-First ----
        /// <summary>
        /// Enumerates the implicit tree described by <paramref name="root"/> and <paramref name="children"/>
        /// in a depth-first manner. For example, this could be used to enumerate the exceptions of an
        /// <see cref="AggregateException"/>:
        /// <code>
        ///     var allExceptions = Traverse.DepthFirst((Exception)new AggregateException(), e => (e as AggregateException)?.InnerExceptions ?? Enumerable.Empty<Exception>());
        /// </code>
        /// </summary>
        public static IEnumerable<T> DepthFirst<T>(T root, Func<T, IEnumerable<T>> children)
        {
            if (children == null) { throw new ArgumentNullException(nameof(children)); }

            return DepthFirstIterator(root, children);
        }

        private static IEnumerable<T> DepthFirstIterator<T>(T root, Func<T, IEnumerable<T>> children)
        {
            var stack = new Stack<T>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var next = stack.Pop();
                yield return next;

                foreach (var child in children(next))
                {
                    stack.Push(child);
                }
            }
        }
        #endregion
    }
}
