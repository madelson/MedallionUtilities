using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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

        #region ---- CollectionEquals ----
        /// <summary>
        /// Determines whether <paramref name="this"/> and <paramref name="that"/> are equal in the sense of having the exact same
        /// elements. Unlike <see cref="Enumerable.SequenceEqual{TSource}(IEnumerable{TSource}, IEnumerable{TSource})"/>,
        /// this method disregards order. Unlike <see cref="ISet{T}.SetEquals(IEnumerable{T})"/>, this method does not disregard duplicates.
        /// An optional <paramref name="comparer"/> allows the equality semantics for the elements to be specified
        /// </summary>
        public static bool CollectionEquals<TElement>(this IEnumerable<TElement> @this, IEnumerable<TElement> that, IEqualityComparer<TElement> comparer = null)
        {
            Throw.IfNull(@this, "this");
            Throw.IfNull(that, "that");

            // FastCount optimization: If both of the collections are materialized and have counts, 
            // we can exit very quickly if those counts differ
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
            // SequenceEqual optimization: we reduce/avoid hashing
            // the collections have common prefixes, at the cost of only one
            // extra Equals() call in the case where the prefixes are not common
            using (var thisEnumerator = @this.GetEnumerator())
            using (var thatEnumerator = that.GetEnumerator())
            {
                while (true)
                {
                    var thisFinished = !thisEnumerator.MoveNext();
                    var thatFinished = !thatEnumerator.MoveNext();

                    if (thisFinished)
                    {
                        // either this shorter than that, or the two were sequence-equal
                        return thatFinished;
                    }
                    if (thatFinished)
                    {
                        // that shorter than this
                        return false;
                    }

                    // keep track of this so that we can factor it into count-based 
                    // logic below
                    ++itemsEnumerated;

                    if (!cmp.Equals(thisEnumerator.Current, thatEnumerator.Current))
                    {
                        break; // prefixes were not equal
                    }
                }

                // now, build a dictionary of item => count out of one collection and then
                // probe it with the other collection to look for mismatches

                // Build/Probe Choice optimization: if we know the count of one collection, we should
                // use the other collection to build the dictionary. That way we can bail immediately if
                // we see too few or too many items
                CountingSet<TElement> elementCounts;
                IEnumerator<TElement> probeSide;
                if (hasThisCount)
                {
                    // we know this's count => use that as the build side
                    probeSide = thisEnumerator;
                    var remaining = thisCount - itemsEnumerated;
                    if (hasThatCount)
                    {
                        // if we have both counts, that means they must be equal or we would have already
                        // exited. However, in this case, we know exactly the capacity needed for the dictionary
                        // so we can avoid resizing
                        elementCounts = new CountingSet<TElement>(capacity: remaining, comparer: cmp);
                        do
                        {
                            elementCounts.Increment(thatEnumerator.Current);
                        }
                        while (thatEnumerator.MoveNext());
                    }
                    else
                    {
                        elementCounts = TryBuildElementCountsWithKnownCount(thatEnumerator, remaining, cmp);
                    }
                }
                else if (TryFastCount(that, out thatCount))
                {
                    // we know that's count => use this as the build side
                    probeSide = thatEnumerator;
                    var remaining = thatCount - itemsEnumerated;
                    elementCounts = TryBuildElementCountsWithKnownCount(thisEnumerator, remaining, cmp);
                }
                else
                {
                    // when we don't know either count, just use that as the build side arbitrarily
                    probeSide = thisEnumerator;
                    elementCounts = new CountingSet<TElement>(cmp);
                    do
                    {
                        elementCounts.Increment(thatEnumerator.Current);
                    }
                    while (thatEnumerator.MoveNext());
                }

                // check whether we failed to construct a dictionary. This happens when we know
                // one of the counts and we detect, during construction, that the counts are unequal
                if (elementCounts == null)
                {
                    return false;
                }
                
                // probe the dictionary with the probe side enumerator
                do
                {
                    if (!elementCounts.TryDecrement(probeSide.Current))
                    {
                        // element in probe not in build => not equal
                        return false;
                    }
                }
                while (probeSide.MoveNext());

                // we are equal only if the loop above completely cleared out the dictionary
                return elementCounts.IsEmpty;
            }
        }

        /// <summary>
        /// Constructs a count dictionary, staying mindful of the known number of elements
        /// so that we bail early (returning null) if we detect a count mismatch
        /// </summary>
        private static CountingSet<TKey> TryBuildElementCountsWithKnownCount<TKey>(
            IEnumerator<TKey> elements, 
            int remaining,
            IEqualityComparer<TKey> comparer)
        {
            if (remaining == 0)
            {
                // don't build the dictionary at all if nothing should be in it
                return null;
            }

            const int MaxInitialElementCountsCapacity = 1024;
            var elementCounts = new CountingSet<TKey>(capacity: Math.Min(remaining, MaxInitialElementCountsCapacity), comparer: comparer);
            elementCounts.Increment(elements.Current);
            while (elements.MoveNext())
            {
                if (--remaining < 0)
                {
                    // too many elements
                    return null;
                }
                elementCounts.Increment(elements.Current);
            }

            if (remaining > 0)
            {
                // too few elements
                return null;
            }

            return elementCounts;
        }
        
        /// <summary>
        /// Key Lookup Reduction optimization: this custom datastructure halves the number of <see cref="IEqualityComparer{T}.GetHashCode(T)"/>
        /// and <see cref="IEqualityComparer{T}.Equals(T, T)"/> operations by building in the increment/decrement operations of a counting dictionary.
        /// This also solves <see cref="Dictionary{TKey, TValue}"/>'s issues with null keys
        /// </summary>
        private sealed class CountingSet<T>
        {
            // picked based on observing unit test performance
            private const double MaxLoad = .62;

            private readonly IEqualityComparer<T> comparer;
            private Bucket[] buckets;
            private int populatedBucketCount;
            /// <summary>
            /// When we reach this count, we need to resize
            /// </summary>
            private int nextResizeCount;            

            public CountingSet(IEqualityComparer<T> comparer, int capacity = 0)
            {
                this.comparer = comparer;
                // we pick the initial length by assuming our current table is one short of the desired
                // capacity and then using our standard logic of picking the next valid table size
                this.buckets = new Bucket[GetNextTableSize((int)(capacity / MaxLoad) - 1)];
                this.nextResizeCount = this.CalculateNextResizeCount();
            }

            public bool IsEmpty { get { return this.populatedBucketCount == 0; } }

            public void Increment(T item)
            {
                int bucketIndex;
                uint hashCode;
                if (this.TryFindBucket(item, out bucketIndex, out hashCode))
                {
                    // if a bucket already existed, just update it's count
                    ++this.buckets[bucketIndex].Count;
                }
                else
                {
                    // otherwise, claim a new bucket
                    this.buckets[bucketIndex].HashCode = hashCode;
                    this.buckets[bucketIndex].Value = item;
                    this.buckets[bucketIndex].Count = 1;
                    ++this.populatedBucketCount;

                    // resize the table if we've grown too full
                    if (this.populatedBucketCount == this.nextResizeCount)
                    {
                        var newBuckets = new Bucket[GetNextTableSize(this.buckets.Length)];

                        // rehash
                        for (var i = 0; i < this.buckets.Length; ++i)
                        {
                            var oldBucket = this.buckets[i];
                            if (oldBucket.HashCode != 0)
                            {
                                var newBucketIndex = oldBucket.HashCode % newBuckets.Length;
                                while (true)
                                {
                                    if (newBuckets[newBucketIndex].HashCode == 0)
                                    {
                                        newBuckets[newBucketIndex] = oldBucket;
                                        break;
                                    }

                                    newBucketIndex = (newBucketIndex + 1) % newBuckets.Length;
                                }
                            } 
                        }

                        this.buckets = newBuckets;
                        this.nextResizeCount = this.CalculateNextResizeCount();
                    }
                }
            }

            public bool TryDecrement(T item)
            {
                int bucketIndex;
                uint ignored;
                if (this.TryFindBucket(item, out bucketIndex, out ignored)
                    && this.buckets[bucketIndex].Count > 0)
                {
                    if (--this.buckets[bucketIndex].Count == 0)
                    {
                        // Note: we can't do this because it messes up our try-find logic
                        //// mark as unpopulated. Not strictly necessary because CollectionEquals always does all increments
                        //// before all decrements currently. However, this is very cheap to do and allowing the collection to
                        //// "just work" in any situation is a nice benefit
                        //// this.buckets[bucketIndex].HashCode = 0;

                        --this.populatedBucketCount;
                    }
                    return true;
                }

                return false;
            }

            private bool TryFindBucket(T item, out int index, out uint hashCode)
            {
                // we convert the raw hash code to a uint to get correctly-signed mod operations
                // and get rid of the zero value so that we can use 0 to mean "unoccupied"
                var rawHashCode = this.comparer.GetHashCode(item);
                hashCode = rawHashCode == 0 ? uint.MaxValue : unchecked((uint)rawHashCode);

                var bestBucketIndex = (int)(hashCode % this.buckets.Length);

                var bucketIndex = bestBucketIndex;
                while (true) // guaranteed to terminate because of how we set load factor
                {
                    var bucket = this.buckets[bucketIndex];
                    if (bucket.HashCode == 0)
                    {
                        // found unoccupied bucket
                        index = bucketIndex;
                        return false;
                    }
                    if (bucket.HashCode == hashCode && this.comparer.Equals(bucket.Value, item))
                    {
                        // found matching bucket
                        index = bucketIndex;
                        return true;
                    }

                    // otherwise march on to the next adjacent bucket
                    bucketIndex = (bucketIndex + 1) % this.buckets.Length;
                }
            }

            private int CalculateNextResizeCount()
            {
                return (int)(MaxLoad * this.buckets.Length) + 1;
            }

            private static readonly int[] HashTableSizes = new[]
            {
                // hash table primes from http://planetmath.org/goodhashtableprimes
                23, 53, 97, 193, 389, 769, 1543, 3079, 6151, 12289,
                24593, 49157, 98317, 196613, 393241, 786433, 1572869,
                3145739, 6291469, 12582917, 25165843, 50331653, 100663319,
                201326611, 402653189, 805306457, 1610612741,
                // the first two values are (1) a prime roughly half way between the previous value and int.MaxValue
                // and (2) the prime closest too, but not above, int.MaxValue. The maximum size is, of course, int.MaxValue
                1879048201, 2147483629, int.MaxValue
            };

            private static int GetNextTableSize(int currentSize)
            {
                for (var i = 0; i < HashTableSizes.Length; ++i)
                {
                    var nextSize = HashTableSizes[i];
                    if (nextSize > currentSize) { return nextSize; }
                }

                throw new InvalidOperationException("Hash table cannot expand further");
            }

            [DebuggerDisplay("{Value}, {Count}, {HashCode}")]
            private struct Bucket
            {
                // note: 0 (default) means the bucket is unoccupied
                internal uint HashCode;
                internal T Value;
                internal int Count;
            }
        }
        #endregion

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
