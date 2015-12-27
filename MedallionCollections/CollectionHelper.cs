using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.Collections
{
    public static class CollectionHelper
    {
        #region ---- Partition ----
        public static IEnumerable<List<T>> Partition<T>(this IEnumerable<T> source, int partitionSize)
        {
            Throw.IfNull(source, nameof(source));
            Throw.IfOutOfRange(partitionSize, nameof(partitionSize), min: 1);

            return PartitionIterator(source, partitionSize);
        }

        private static IEnumerable<List<T>> PartitionIterator<T>(this IEnumerable<T> source, int partitionSize)
        {
            // we like initializing our lists with capacity to avoid resizes. However, we don't want to trigger
            // OutOfMemory if the partition size is huge
            var initialCapacity = Math.Max(partitionSize, 1024);

            using (var enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    var partition = new List<T>(capacity: initialCapacity) { enumerator.Current };
                    for (var i = 1; i < partitionSize && enumerator.MoveNext(); ++i)
                    {
                        partition.Add(enumerator.Current);
                    }
                    yield return partition;
                }
            }
        }
        #endregion

        #region ---- Append ----
        public static IEnumerable<TElement> Append<TElement>(this IEnumerable<TElement> first, IEnumerable<TElement> second)
        {
            Throw.IfNull(first, nameof(first));
            Throw.IfNull(second, nameof(second));

            return new AppendEnumerable<TElement>(first, second);
        }

        public static IEnumerable<TElement> Append<TElement>(this IEnumerable<TElement> sequence, TElement next)
        {
            Throw.IfNull(sequence, nameof(sequence));

            return new AppendOneEnumerable<TElement>(sequence, next);
        }

        public static IEnumerable<TElement> Prepend<TElement>(this IEnumerable<TElement> second, IEnumerable<TElement> first)
        {
            return first.Append(second);
        }

        public static IEnumerable<TElement> Prepend<TElement>(this IEnumerable<TElement> sequence, TElement previous)
        {
            Throw.IfNull(sequence, nameof(sequence));

            return new PrependOneEnumerable<TElement>(previous, sequence);
        }

        private interface IAppendEnumerable<out TElement>
        {
            IEnumerable<TElement> PreviousElements { get; }
            TElement PreviousElement { get; }
            IEnumerable<TElement> NextElements { get; }
            TElement NextElement { get; }
        }

        private abstract class AppendEnumerableBase<TElement> : IAppendEnumerable<TElement>, IEnumerable<TElement>
        {
            public abstract TElement NextElement { get; }
            public abstract IEnumerable<TElement> NextElements { get; }
            public abstract TElement PreviousElement { get; }
            public abstract IEnumerable<TElement> PreviousElements { get; }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.AsEnumerable().GetEnumerator();
            }

            IEnumerator<TElement> IEnumerable<TElement>.GetEnumerator()
            {
                // we special case the basic case so that it doesn't even need to create the stack
                if (!(this.PreviousElements is IAppendEnumerable<TElement>)
                    && !(this.NextElements is IAppendEnumerable<TElement>))
                {
                    if (this.PreviousElements != null)
                    {
                        foreach (var element in this.PreviousElements)
                        {
                            yield return element;
                        }
                    }
                    else
                    {
                        yield return this.PreviousElement;
                    }

                    if (this.NextElements != null)
                    {
                        foreach (var element in this.NextElements)
                        {
                            yield return element;
                        }
                    }
                    else
                    {
                        yield return this.NextElement;
                    }
                }

                // the algorithm here keeps 2 pieces of state:
                // (1) the current node in the append enumerable binary tree
                // (2) a stack of nodes we have to come back to to process the right subtree (nexts)
                // the steps are as follows, starting with current as the root of the tree:
                // (1) if the left subtree is a leaf, yield it
                // (2) otherwise, push current on the stack and set current = the left subtree
                // (3) if the right subtree is a leaf, yield it
                // (4) otherwise, set current = right subtree
                // (5) if both subtrees were leaves, set current = stack.Pop(), or exit if stack is empty

                IAppendEnumerable<TElement> currentAppendEnumerable = this;
                var enumerableStack = new Stack<IAppendEnumerable<TElement>>();
                while (true)
                {
                    if (currentAppendEnumerable != null)
                    {
                        var previous = currentAppendEnumerable.PreviousElements;
                        if (previous == null)
                        {
                            yield return currentAppendEnumerable.PreviousElement;
                        }
                        else
                        {
                            var previousAppendEnumerable = previous as IAppendEnumerable<TElement>;
                            if (previousAppendEnumerable != null)
                            {
                                enumerableStack.Push(currentAppendEnumerable);
                                currentAppendEnumerable = previousAppendEnumerable;
                                continue;
                            }

                            foreach (var previousElement in currentAppendEnumerable.PreviousElements)
                            {
                                yield return previousElement;
                            }
                        }
                    }

                    if (currentAppendEnumerable == null)
                    {
                        if (enumerableStack.Count == 0)
                        {
                            yield break;
                        }
                        currentAppendEnumerable = enumerableStack.Pop();
                    }

                    var next = currentAppendEnumerable.NextElements;
                    if (next == null)
                    {
                        yield return currentAppendEnumerable.NextElement;
                    }
                    else
                    {
                        var nextAppendEnumerable = currentAppendEnumerable.NextElements as IAppendEnumerable<TElement>;
                        if (nextAppendEnumerable != null)
                        {
                            currentAppendEnumerable = nextAppendEnumerable;
                            continue;
                        }

                        foreach (var nextElement in currentAppendEnumerable.NextElements)
                        {
                            yield return nextElement;
                        }
                    }

                    currentAppendEnumerable = null;
                }
            }
        }

        private sealed class AppendEnumerable<TElement> : AppendEnumerableBase<TElement>
        {
            private readonly IEnumerable<TElement> previous, next;

            public AppendEnumerable(IEnumerable<TElement> previous, IEnumerable<TElement> next)
            {
                this.previous = previous;
                this.next = next;
            }

            public override TElement NextElement { get { throw new InvalidOperationException(); } }
            public override IEnumerable<TElement> NextElements { get { return this.next; } }
            public override TElement PreviousElement { get { throw new InvalidOperationException(); } }
            public override IEnumerable<TElement> PreviousElements { get { return this.previous; } }
        }

        private sealed class AppendOneEnumerable<TElement> : AppendEnumerableBase<TElement>
        {
            private readonly IEnumerable<TElement> previous;
            private readonly TElement next;

            public AppendOneEnumerable(IEnumerable<TElement> previous, TElement next)
            {
                this.previous = previous;
                this.next = next;
            }

            public override TElement NextElement { get { return this.next; } }
            public override IEnumerable<TElement> NextElements { get { return null; } }
            public override TElement PreviousElement { get { throw new InvalidOperationException(); } }
            public override IEnumerable<TElement> PreviousElements { get { return this.previous; } }
        }

        private sealed class PrependOneEnumerable<TElement> : AppendEnumerableBase<TElement>
        {
            private readonly TElement previous;
            private readonly IEnumerable<TElement> next;

            public PrependOneEnumerable(TElement previous, IEnumerable<TElement> next)
            {
                this.previous = previous;
                this.next = next;
            }

            public override TElement NextElement { get { throw new InvalidOperationException(); } }
            public override IEnumerable<TElement> NextElements { get { return this.next; } }
            public override TElement PreviousElement { get { return this.previous; ; } }
            public override IEnumerable<TElement> PreviousElements { get { return null; } }
        }
        #endregion

        //#region ---- AtLeast / AtMost ----
        //public static bool HasAtLeast<T>(this IEnumerable<T> source, int count)
        //{
        //    Throw.IfNull(source, nameof(source));
        //    Throw.IfOutOfRange(count, nameof(count), min: 0);

        //    int knownCount;
        //    return TryFastCount(source, out knownCount)
        //        ? knownCount >= count
        //        : source.Take(count).Count() == count;
        //}

        //public static bool HasAtMost<T>(this IEnumerable<T> source, int count)
        //{
        //    Throw.IfNull(source, nameof(source));
        //    Throw.IfOutOfRange(count, nameof(count), min: 0, max: int.MaxValue - 1);

        //    int knownCount;
        //    return TryFastCount(source, out knownCount)
        //        ? knownCount <= count
        //        : source.Take(count + 1).Count() <= count;
        //}
        //#endregion

        #region ---- MaxBy / MinBy ----
        public static T MaxBy<T, TKey>(this IEnumerable<T> source, Func<T, TKey> keySelector, IComparer<TKey> comparer = null)
        {
            Throw.IfNull(source, nameof(source));
            Throw.IfNull(keySelector, nameof(keySelector));

            var comparerToUse = comparer ?? Comparer<TKey>.Default;

            var maxKey = default(TKey);
            var max = default(T);
            if (maxKey == null)
            {
                foreach (var item in source)
                {
                    var key = keySelector(item);
                    if (comparer.Compare(key, maxKey) > 0)
                    {
                        maxKey = key;
                        max = item;
                    }
                }
            }
            else
            {
                var hasValue = false;
                foreach (var item in source)
                {
                    var key = keySelector(item);
                    if (hasValue)
                    {
                        if (comparer.Compare(key, maxKey) > 0)
                        {
                            maxKey = key;
                            max = item;
                        }
                    }
                    else
                    {
                        maxKey = key;
                        max = item;
                    }
                }
                if (!hasValue)
                {
                    throw new InvalidOperationException("Sequence contains no elements");
                }
            }

            return max;
        }

        #endregion
        // TODO maxby, minby

        // ideas:
        // start with sequence compare
        // could revert to sequence compare when dictionary is balanced
        // could build dictionary inline & remove zeros
        public static bool CollectionEquals<TElement>(this IEnumerable<TElement> @this, IEnumerable<TElement> that, IEqualityComparer<TElement> comparer = null)
        {
            Throw.IfNull(@this, "this");
            Throw.IfNull(that, "that");

            int thisCount, thatCount;
            var hasThisCount = TryFastCount(@this, out thisCount);
            bool hasThatCount;
            if (hasThisCount)
            {
                hasThatCount = TryFastCount(that, out thatCount);
                if (hasThatCount)
                {
                    if (thisCount != thatCount)
                    {
                        return false;
                    }
                    if (thisCount == 0)
                    {
                        return true;
                    }
                }
            }
            else
            {
                hasThatCount = false;
            }

            var cmp = comparer ?? EqualityComparer<TElement>.Default;

            var itemsEnumerated = 0;
            using (var thisEnumerator = @this.GetEnumerator())
            using (var thatEnumerator = that.GetEnumerator())
            {
                while (true)
                {
                    var thisFinished = !thisEnumerator.MoveNext();
                    var thatFinished = !thatEnumerator.MoveNext();

                    if (thisFinished)
                    {
                        return thatFinished;
                    }
                    if (thatFinished)
                    {
                        return false;
                    }

                    ++itemsEnumerated;

                    if (!cmp.Equals(thisEnumerator.Current, thatEnumerator.Current))
                    {
                        break;
                    }
                }

                Dictionary<TElement, int> elementCounts;
                IEnumerator<TElement> probeSide;
                if (hasThisCount)
                {
                    probeSide = thisEnumerator;
                    var remaining = thisCount - itemsEnumerated;
                    if (hasThatCount)
                    {
                        elementCounts = new Dictionary<TElement, int>(capacity: remaining, comparer: cmp)
                        {
                            { thatEnumerator.Current, 1 }
                        };
                        while (thatEnumerator.MoveNext())
                        {
                            elementCounts.IncrementCount(thatEnumerator.Current);
                        }
                    }
                    else
                    {
                        elementCounts = TryBuildElementCountsWithKnownCount(thatEnumerator, remaining);
                    }
                }
                else if (TryFastCount(that, out thatCount))
                {
                    probeSide = thatEnumerator;
                    var remaining = thatCount - itemsEnumerated;
                    elementCounts = TryBuildElementCountsWithKnownCount(thisEnumerator, remaining);
                }
                else
                {
                    probeSide = thisEnumerator;
                    elementCounts = new Dictionary<TElement, int>(cmp)
                    {
                        { thatEnumerator.Current, 1 }
                    };
                    while (thatEnumerator.MoveNext())
                    {
                        elementCounts.IncrementCount(thatEnumerator.Current);
                    }
                }

                if (elementCounts == null)
                {
                    return false;
                }

                do
                {
                    if (!elementCounts.TryDecrementCount(probeSide.Current))
                    {
                        return false;
                    }
                }
                while (probeSide.MoveNext());

                return elementCounts.Count == 0;
            }
        }

        private static Dictionary<TKey, int> TryBuildElementCountsWithKnownCount<TKey>(
            IEnumerator<TKey> elements, 
            int remaining)
        {
            if (remaining == 0)
            {
                return null;
            }

            const int MaxInitialElementCountsCapacity = 1024;
            var elementCounts = new Dictionary<TKey, int>(capacity: Math.Min(remaining, MaxInitialElementCountsCapacity))
            {
                { elements.Current, 1 }
            };
            while (elements.MoveNext())
            {
                if (--remaining < 0)
                {
                    return null;
                }
                elementCounts.IncrementCount(elements.Current);
            }

            if (remaining > 0)
            {
                return null;
            }

            return elementCounts;
        }

        private static void IncrementCount<TKey>(this Dictionary<TKey, int> elementCounts, TKey key)
        {
            int existingCount;
            if (elementCounts.TryGetValue(key, out existingCount))
            {
                elementCounts[key] = existingCount + 1;
            }
            else
            {
                elementCounts.Add(key, 1);
            }
        }

        private static bool TryDecrementCount<TKey>(this Dictionary<TKey, int> elementCounts, TKey key)
        {
            int existingCount;
            if (!elementCounts.TryGetValue(key, out existingCount))
            {
                return false;
            }

            if (existingCount == 1)
            {
                elementCounts.Remove(key);
            }
            else
            {
                elementCounts[key] = existingCount - 1;
            }
            
            return true;
        }

        private static bool TryFastCount<T>(IEnumerable<T> @this, out int count)
        {
            var collection = @this as ICollection<T>;
            if (collection != null)
            {
                count = collection.Count;
                return true;
            }

            var readOnlyCollection = @this as IReadOnlyCollection<T>;
            if (readOnlyCollection != null)
            {
                count = readOnlyCollection.Count;
                return true;
            }

            // TODO should we test for ICollection here?

            count = -1;
            return false;
        }
    }
}
