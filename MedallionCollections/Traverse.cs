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
        ///     var allExceptions = Traverse.BreadthFirst((Exception)new AggregateException(), e => (e as AggregateException)?.InnerExceptions ?? Enumerable.Empty&lt;Exception&gt;());
        /// </code>
        /// </summary>
        public static IEnumerable<T> BreadthFirst<T>(T root, Func<T, IEnumerable<T>> children)
        {
            if (children == null) { throw new ArgumentNullException(nameof(children)); }

            return BreadthFirstIterator(root, children);
        }

        private static IEnumerable<T> BreadthFirstIterator<T>(T root, Func<T, IEnumerable<T>> children)
        {
            // note that this implementation has two nice properties which require a bit more complexity
            // in the code: (1) children are yielded in order and (2) child enumerators are fully lazy

            yield return root;
            var queue = new Queue<IEnumerable<T>>();
            queue.Enqueue(children(root));

            do
            {
                foreach (var child in queue.Dequeue())
                {
                    yield return child;
                    queue.Enqueue(children(child));
                }
            }
            while (queue.Count > 0);
        }
        #endregion

        #region ---- Depth-First ----
        /// <summary>
        /// Enumerates the implicit tree described by <paramref name="root"/> and <paramref name="children"/>
        /// in a depth-first manner. For example, this could be used to enumerate the exceptions of an
        /// <see cref="AggregateException"/>:
        /// <code>
        ///     var allExceptions = Traverse.DepthFirst((Exception)new AggregateException(), e => (e as AggregateException)?.InnerExceptions ?? Enumerable.Empty&lt;Exception&gt;());
        /// </code>
        /// </summary>
        public static IEnumerable<T> DepthFirst<T>(T root, Func<T, IEnumerable<T>> children)
        {
            if (children == null) { throw new ArgumentNullException(nameof(children)); }

            return DepthFirstIterator(root, children);
        }

        private static IEnumerable<T> DepthFirstIterator<T>(T root, Func<T, IEnumerable<T>> children)
        {
            // note that this implementation has two nice properties which require a bit more complexity
            // in the code: (1) children are yielded in order and (2) child enumerators are fully lazy

            var current = root;
            var stack = new Stack<IEnumerator<T>>();

            try
            {
                while (true)
                {
                    yield return current;
                    
                    var childrenEnumerator = children(current).GetEnumerator();
                    if (childrenEnumerator.MoveNext())
                    {
                        // if we have children, the first child is our next current
                        // and push the new enumerator
                        current = childrenEnumerator.Current;
                        stack.Push(childrenEnumerator);
                    }
                    else
                    {
                        // otherwise, cleanup the empty enumerator and...
                        childrenEnumerator.Dispose();

                        // search up the stack for an enumerator with elements left
                        while (true)
                        {
                            if (stack.Count == 0)
                            {
                                // we didn't find one, so we're all done
                                yield break;
                            }

                            var topEnumerator = stack.Peek();
                            if (topEnumerator.MoveNext())
                            {
                                current = topEnumerator.Current;
                                break;
                            }
                            else
                            {
                                stack.Pop().Dispose();
                            }
                        }
                    }
                }
            }
            finally
            {
                // guarantee that everything is cleaned up even
                // if we don't enumerate all the way through
                while (stack.Count > 0)
                {
                    stack.Pop().Dispose();
                }
            }
        }
        #endregion
    }
}
