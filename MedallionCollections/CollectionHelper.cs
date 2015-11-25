using System;
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

        #region ---- AtLeast / AtMost ----
        public static bool HasAtLeast<T>(this IEnumerable<T> source, int count)
        {
            Throw.IfNull(source, nameof(source));
            Throw.IfOutOfRange(count, nameof(count), min: 0);

            return source.Take(count).Count() == count;
        }

        public static bool HasAtMost<T>(this IEnumerable<T> source, int count)
        {
            Throw.IfNull(source, nameof(source));
            Throw.IfOutOfRange(count, nameof(count), min: 0, max: int.MaxValue - 1);
            // todo tryfastcount
            return source.Take(count + 1).Count() <= count;
        }
        #endregion

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
            var hasThatCount = TryFastCount(that, out thatCount);

            int maxRemainingItemsToProcess;
            if (hasThisCount)
            {
                if (hasThatCount && thisCount != thatCount)
                {
                    return false;
                }
                maxRemainingItemsToProcess = thisCount;
            }
            else if (hasThatCount)
            {
                maxRemainingItemsToProcess = thatCount;
            }
            else
            {
                maxRemainingItemsToProcess = -1;
            }

            var cmp = comparer ?? EqualityComparer<TElement>.Default;

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

                    if (!cmp.Equals(thisEnumerator.Current, thatEnumerator.Current))
                    {
                        break;
                    }

                    if (maxRemainingItemsToProcess >= 0)
                    {
                        --maxRemainingItemsToProcess;
                    }
                }

                var dictionary = new Dictionary<TElement, int>(cmp);
                do
                {
                    var thisValue = thisEnumerator.Current;
                    var thatValue = thatEnumerator.Current;
                    int thisValueCount, thatValueCount;
                    
                    if (dictionary.TryGetValue(thisValue, out thisValueCount))
                    {
                        var newCount = thisValueCount + 1;
                        if (newCount == 0)
                        {
                            dictionary.Remove(thisValue);
                        }
                        else
                        {
                            dictionary[thisValue] = newCount;
                        }
                    }
                    else
                    {
                        dictionary.Add(thisValue, 1);
                    }


                }
                while (true);
            }
        }

        private static int UpdateEqualityCheckDictionary<TElement>(TElement element, Dictionary<TElement, int> dictionary, ref int nullCount, int increment)
        {
            if (element == null)
            {
                var result = increment < 0 
                    ? nullCount < 0
                nullCount += increment;
                return 
            }
        }

        private static int GetEqualityDebtDelta(int count, int increment)
        {
            return count < 0 
                ? (increment < 0 ? -1 : 1)
                : 
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
