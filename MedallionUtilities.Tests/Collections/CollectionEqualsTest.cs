using Medallion.Random;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using System.Collections;

namespace Medallion.Collections
{
    public class CollectionEqualsTest
    {
        private readonly ITestOutputHelper output;

        public CollectionEqualsTest(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void TestEqualSequences()
        {
            new[] { 1, 2, 3 }.CollectionEquals(new[] { 1, 2, 3 }).ShouldEqual(true);
            Enumerable.Range(0, 10000).CollectionEquals(Enumerable.Range(0, 10000)).ShouldEqual(true);
            Sequence("a", "b", "c").CollectionEquals(Sequence("A", "B", "C"), StringComparer.OrdinalIgnoreCase).ShouldEqual(true);
        }

        [Fact]
        public void TestRapidExit()
        {
            var comparer = EqualityComparers.Create(
                (int a, int b) => { throw new InvalidOperationException("should never get here"); },
                i => { throw new InvalidOperationException("should never get here"); }
            );

            Sequence<int>().CollectionEquals(Sequence<int>(), comparer).ShouldEqual(true);
            new int[0].CollectionEquals(new int[0], comparer).ShouldEqual(true);

            new[] { 1, 2, 3 }.CollectionEquals(new[] { 1, 2, 3, 4 }).ShouldEqual(false);
        } 

        [Fact]
        public void TestNullElements()
        {
            Sequence<int?>(1, 2, 3, 4, null).CollectionEquals(Sequence<int?>(1, 2, 3, 4, null)).ShouldEqual(true);
            Sequence<int?>(1, 2, 3, 4, null).CollectionEquals(Sequence<int?>(null, 2, 3, 1, 5)).ShouldEqual(false);
            Sequence<int?>(null, null).CollectionEquals(Sequence<int?>(null, null)).ShouldEqual(true);
            Sequence<int?>(null, null, null).CollectionEquals(new int?[] { null, null }).ShouldEqual(false);
        }

        [Fact]
        public void TestOutOfOrder()
        {
            Sequence("Apple", "Banana", "Carrot").CollectionEquals(Sequence("carrot", "banana", "apple"), StringComparer.OrdinalIgnoreCase)
                .ShouldEqual(true);
        }

        [Fact]
        public void TestDuplicates()
        {
            Enumerable.Repeat('a', 1000).CollectionEquals(Enumerable.Repeat('a', 1000)).ShouldEqual(true);
            Enumerable.Repeat('a', 1000).Concat(Enumerable.Repeat('b', 1000))
                .CollectionEquals(Enumerable.Repeat('b', 1000).Concat(Enumerable.Repeat('a', 1000)))
                .ShouldEqual(true);

            new[] { 1, 1, 2, 2, 3, 3, }.CollectionEquals(Sequence(1, 2, 3, 1, 2, 3)).ShouldEqual(true);
            new[] { 1, 1, 2, 2, 3, 3, }.CollectionEquals(Sequence(1, 2, 1, 2, 3, 4)).ShouldEqual(false);
        }
        
        [Fact]
        public void TestSmartBuildSideProbeSideChoice()
        {
            var longerButThrows = new CountingEnumerableCollection<int>(ThrowsAt(Enumerable.Range(0, 10), index: 9), count: 10);
            var shorter = Enumerable.Range(0, 9).Reverse(); // force us into build/probe mode

            longerButThrows.CollectionEquals(shorter).ShouldEqual(false);
            shorter.CollectionEquals(longerButThrows).ShouldEqual(false);
        }

        // TODO we could add more explicit case tests to get full coverage of all branches, but
        // for now we do get that via fuzz test

        [Fact]
        public void FuzzTest()
        {
            var random = new System.Random(12345);

            for (var i = 0; i < 10000; ++i)
            {
                var count = random.Next(1000);
                var sequence1 = Enumerable.Range(0, count).Select(_ => random.Next(count)).ToList();
                var sequence2 = sequence1.Shuffled(random).ToList();
                var equal = random.NextBoolean();
                if (!equal)
                {
                    switch (count == 0 ? 4 : random.Next(5))
                    {
                        case 0:
                            sequence2[random.Next(count)]++;
                            break;
                        case 1:
                            var toChange = random.Next(1, count + 1);
                            for (var j = 0; j < toChange; ++j)
                            {
                                sequence2[j]++;
                            }
                            break;
                        case 2:
                            sequence2.RemoveAt(random.Next(count));
                            break;
                        case 3:
                            var toRemove = random.Next(1, count + 1);
                            sequence2 = sequence2.Skip(toRemove).ToList();
                            break;
                        case 4:
                            var toAdd = random.Next(1, count + 1);
                            sequence2.AddRange(Enumerable.Repeat(random.Next(count), toAdd));
                            break;
                        default:
                            throw new InvalidOperationException("should never get here");
                    }
                }

                var toCompare1 = random.NextBoolean() ? sequence1 : sequence1.Where(_ => true);
                var toCompare2 = random.NextBoolean() ? sequence2 : sequence2.Where(_ => true);
                
                try
                {
                    toCompare1.CollectionEquals(toCompare2)
                        .ShouldEqual(equal);
                }
                catch
                {
                    this.output.WriteLine($"Case {i} failed");
                    throw;
                }
            }
        }

        [Fact]
        public void ComparisonTest()
        {
            // large sequence-equal collections
            var a = Enumerable.Range(0, 1000);
            var b = a.ToArray();

            long enumerateCount, equalsCount, hashCount,
                dictEnumerateCount, dictEqualsCount, dictHashCount,
                sortEnumerateCount, sortEqualsCount, sortHashCount;

            ProfiledEquals(a, b, CollectionHelper.CollectionEquals<int>, out enumerateCount, out equalsCount, out hashCount).ShouldEqual(true);
            ProfiledEquals(a, b, DictionaryBasedEquals<int>, out dictEnumerateCount, out dictEqualsCount, out dictHashCount);
            ProfiledEquals(a, b, SortBasedEquals<int>, out sortEnumerateCount, out sortEqualsCount, out sortHashCount);
            this.output.WriteLine($@"
                ce: {enumerateCount}, {equalsCount}, {hashCount}
                dict: {dictEnumerateCount}, {dictEqualsCount}, {dictHashCount}
                sort: {sortEnumerateCount}, {sortEqualsCount}, {sortHashCount}"
            );
        }

        private static bool ProfiledEquals<T>(
            IEnumerable<T> a, 
            IEnumerable<T> b,
            Func<IEnumerable<T>, IEnumerable<T>, IEqualityComparer<T>, bool> equals,
            out long enumerateCount,
            out long equalsCount,
            out long hashCount)
        {
            var wrappedA = new CountingEnumerable<T>(a);
            var wrappedB = b is IReadOnlyCollection<T> ? new CountingEnumerableCollection<T>((IReadOnlyCollection<T>)b) : new CountingEnumerable<T>(b);
            var comparer = new CountingEqualityComparer<T>();
            var result = equals(wrappedA, wrappedB, comparer);

            enumerateCount = wrappedA.EnumerateCount + wrappedB.EnumerateCount;
            equalsCount = comparer.EqualsCount;
            hashCount = comparer.HashCount;
            return result;
        }

        private static bool SortBasedEquals<T>(IEnumerable<T> a, IEnumerable<T> b, IEqualityComparer<T> comparer)
        {
            var order = Comparers.Create((T item) => comparer.GetHashCode(item));
            return a.OrderBy(x => x, order).SequenceEqual(b.OrderBy(x => x, order), comparer);
        }

        private static bool DictionaryBasedEquals<T>(IEnumerable<T> a, IEnumerable<T> b, IEqualityComparer<T> comparer)
        {
            var dictionary = new Dictionary<T, int>(comparer);
            foreach (var item in a)
            {
                int existingCount;
                if (dictionary.TryGetValue(item, out existingCount))
                {
                    dictionary[item] = existingCount + 1;
                }
                else
                {
                    dictionary.Add(item, 1);
                }
            }

            foreach (var item in b)
            {
                int count;
                if (!dictionary.TryGetValue(item, out count))
                {
                    return false;
                }
                if (count == 1)
                {
                    dictionary.Remove(item);
                }
                else
                {
                    dictionary[item] = count - 1;
                }
            }

            return dictionary.Count == 0;
        }

        private sealed class CountingEqualityComparer<T> : IEqualityComparer<T>
        {
            public long EqualsCount { get; private set; }
            public long HashCount { get; private set; }

            bool IEqualityComparer<T>.Equals(T x, T y)
            {
                this.EqualsCount++;
                return EqualityComparer<T>.Default.Equals(x, y);
            }

            int IEqualityComparer<T>.GetHashCode(T obj)
            {
                this.HashCount++;
                return EqualityComparer<T>.Default.GetHashCode(obj);
            }
        }

        private class CountingEnumerable<T> : IEnumerable<T>
        {
            private readonly IEnumerable<T> enumerable;

            public CountingEnumerable(IEnumerable<T> enumerable)
            {
                this.enumerable = enumerable;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }

            public long EnumerateCount { get; private set; }

            public IEnumerator<T> GetEnumerator()
            {
                foreach (var item in this.enumerable)
                {
                    this.EnumerateCount++;
                    yield return item;
                }
            }
        }

        private class CountingEnumerableCollection<T> : CountingEnumerable<T>, IReadOnlyCollection<T>
        {
            public int Count { get; private set; }

            public CountingEnumerableCollection(IEnumerable<T> sequence, int count)
                : base(sequence)
            {
                this.Count = count;
            }

            public CountingEnumerableCollection(IReadOnlyCollection<T> collection)
               : this(collection, collection.Count)
            {
            }
        }

        private static IEnumerable<T> Sequence<T>(params T[] items) { return items.Where(i => true); }

        private static IEnumerable<T> ThrowsAt<T>(IEnumerable<T> items, int index)
        {
            var i = 0;
            foreach (var item in items)
            {
                if (i == index)
                {
                    Assert.False(true, "ThrowsAt failure!");
                }
                yield return item;
                ++i;
            }
        }
    }
}
