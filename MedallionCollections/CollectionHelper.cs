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
        //public static IEnumerable<TElement> Append<TElement>(this IEnumerable<TElement> first, IEnumerable<TElement> second)
        //{

        //}

        //public static IEnumerable<TElement> Append<TElement>(this IEnumerable<TElement> sequence, TElement next)
        //{

        //}

        private static IEnumerator<TElement> AppendEnumerableIterator<TElement>(IAppendEnumerable<TElement> append)
        {
            // special case when we don't have nesting to avoid allocating the stack
            if (!(append.First is IAppendEnumerable<TElement>)
                && !(append.Second is IAppendEnumerable<TElement>))
            {
                foreach (var element in append.First)
                {
                    yield return element;
                }
                if (append.Second != null)
                {
                    yield return append.Next;
                }
                else
                {
                    foreach (var element in append.Second)
                    {
                        yield return element;
                    }
                }

                yield break;
            }

            var stack = new Stack<IAppendEnumerable<TElement>>();
            stack.Push(append);
            do
            {
                GatherFirstEnumerables(stack.Peek().First, stack);

                var nextAppend = stack.Pop();
                foreach (var item in nextAppend.First)
                {
                    yield return item;
                }


            }
            while (stack.Count > 0);
        }

        private static void GatherFirstEnumerables<TElement>(IEnumerable<TElement> sequence, Stack<IAppendEnumerable<TElement>> stack)
        {
            for (var append = sequence as IAppendEnumerable<TElement>; 
                append != null; 
                append = append.First as IAppendEnumerable<TElement>)
            {
                stack.Push(append);
            }
        }

        private interface IAppendEnumerable<out TElement> : IEnumerable<TElement>
        {
            IEnumerable<TElement> First { get; }
            IEnumerable<TElement> Second { get; }
            TElement Next { get; }
        }

        private sealed class FirstSecondAppendEnumerable<TElement> : IAppendEnumerable<TElement>
        {
            public FirstSecondAppendEnumerable(IEnumerable<TElement> first, IEnumerable<TElement> second)
            {
                this.First = first;
                this.Second = second;
            }

            public IEnumerable<TElement> First { get; private set; }
            public IEnumerable<TElement> Second { get; private set; }
            TElement IAppendEnumerable<TElement>.Next { get { throw new NotSupportedException(); } }

            public IEnumerator<TElement> GetEnumerator()
            {
                return AppendEnumerableIterator(this);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
        }

        private sealed class FirstNextAppendEnumerable<TElement> : IAppendEnumerable<TElement>
        {
            public FirstNextAppendEnumerable(IEnumerable<TElement> sequence, TElement next)
            {
                this.First = sequence;
                this.Next = next;
            }

            public IEnumerable<TElement> First { get; private set; }
            IEnumerable<TElement> IAppendEnumerable<TElement>.Second { get { return null; } }
            public TElement Next { get; private set; }

            public IEnumerator<TElement> GetEnumerator()
            {
                return AppendEnumerableIterator(this);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
        }

        private sealed class AppendEnumerable<TElement> : IEnumerable<TElement>
        {
            private readonly IEnumerable<TElement> first, second;
            private readonly TElement next;

            public AppendEnumerable(IEnumerable<TElement> first, IEnumerable<TElement> second)
            {
                this.first = first;
                this.second = second;
            }

            public AppendEnumerable(IEnumerable<TElement> first, TElement next)
            {
                this.first = first;
                this.next = next;
            }

            public IEnumerator<TElement> GetEnumerator()
            {
                var firstAppendEnumerable = this.first as AppendEnumerable<TElement>;
                throw new NotImplementedException();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
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

            count = -1;
            return false;
        }
    }
}
