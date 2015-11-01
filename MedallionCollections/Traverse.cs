using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.Collections
{
    public static class Traverse
    {
        #region ---- Along ----
        public static IEnumerable<T> Along<T>(T root, Func<T, T> next)
            where T : class
        {
            Throw.IfNull(next, "next");

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
        public static IEnumerable<T> BreadthFirst<T>(T root, Func<T, IEnumerable<T>> children)
        {
            Throw.IfNull(children, "children");

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
        public static IEnumerable<T> DepthFirst<T>(T root, Func<T, IEnumerable<T>> children)
        {
            Throw.IfNull(children, "children");

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

        // todo distinct depth, breadth, pre-post order?
    }
}
