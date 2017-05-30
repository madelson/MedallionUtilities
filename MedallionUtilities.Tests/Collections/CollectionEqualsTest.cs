using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using System.Collections;
using System.Threading;
using System.Diagnostics;

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
        public void TestBadArguments()
        {
            Assert.Throws<ArgumentNullException>(() => default(IEnumerable<int>).CollectionEquals(new[] { 1 }));
            Assert.Throws<ArgumentNullException>(() => new[] { 1 }.CollectionEquals(null));
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
        public void TestCustomComparer()
        {
            new[] { "a", "b" }.CollectionEquals(Sequence("B", "A"), StringComparer.OrdinalIgnoreCase)
                .ShouldEqual(true);
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

        #region ---- Comparsion Stuff ----
        [Fact]
        public void ComparisonTest()
        {
            var results = new Dictionary<string, ComparisonResult>();

            results.Add("arrays of different lengths", ComparisonProfile(Enumerable.Range(0, 1000).ToArray(), Enumerable.Range(0, 1001).ToArray()));

            results.Add("long array short lazy", ComparisonProfile(Enumerable.Range(0, 1000).Reverse().ToArray(), Enumerable.Range(0, 500)));

            results.Add("short array long lazy", ComparisonProfile(Enumerable.Range(0, 1000).Reverse(), Enumerable.Range(0, 500).ToArray()));

            results.Add("sequence equal", ComparisonProfile(Enumerable.Range(0, 1000), Enumerable.Range(0, 1000)));

            results.Add("mostly sequence equal", ComparisonProfile(Enumerable.Range(0, 1000).Append(int.MaxValue), Enumerable.Range(0, 1000).Append(int.MaxValue)));

            results.Add("equal out of order", ComparisonProfile(Enumerable.Range(0, 1000), Enumerable.Range(0, 1000).OrderByDescending(i => i).ToArray()));

            var strings = Enumerable.Range(0, 1000).Select(i => (i + (long)int.MaxValue).ToString("0000000000000000000"))
                .ToArray();
            results.Add("strings equal out of order", ComparisonProfile(strings, strings.Reverse()));

            foreach (var kvp in results)
            {
                this.output.WriteLine($"---- {kvp.Key} ----");

                var cer = kvp.Value.CollectionEqualsResult;
                var dmr = kvp.Value.DictionaryMethodResult;
                Func<double, double, string> perc = (n, d) => (n / d).ToString("0.0%");
                this.output.WriteLine($"{perc(cer.Duration.Ticks, dmr.Duration.Ticks)}, {perc(cer.EnumerateCount, dmr.EnumerateCount)} {perc(cer.EqualsCount, dmr.EqualsCount)}, {perc(cer.HashCount, dmr.HashCount)}");
                this.output.WriteLine($"CollectionEquals: {kvp.Value.CollectionEqualsResult}");
                this.output.WriteLine($"Dictionary: {kvp.Value.DictionaryMethodResult}");
                //this.output.WriteLine($"Sort: {kvp.Value.SortMethodResult}");

                kvp.Value.CollectionEqualsResult.AssertBetterThan(kvp.Value.DictionaryMethodResult);
                //kvp.Value.CollectionEqualsResult.AssertBetterThan(kvp.Value.SortMethodResult);
            }
        }

        private static ComparisonResult ComparisonProfile<T>(IEnumerable<T> a, IEnumerable<T> b)
        {
            return new ComparisonResult
            {
                CollectionEqualsResult = Profile(a, b, CollectionHelper.CollectionEquals),
                DictionaryMethodResult = Profile(a, b, DictionaryBasedEquals),
                //SortMethodResult = Profile(a, b, SortBasedEquals),
            };
        }

        private static ProfilingResult Profile<T>(
            IEnumerable<T> a,
            IEnumerable<T> b,
            Func<IEnumerable<T>, IEnumerable<T>, IEqualityComparer<T>, bool> equals)
        {
            // capture base stats
            var wrappedA = a is IReadOnlyCollection<T> ? new CountingEnumerableCollection<T>((IReadOnlyCollection<T>)a) : new CountingEnumerable<T>(a);
            var wrappedB = b is IReadOnlyCollection<T> ? new CountingEnumerableCollection<T>((IReadOnlyCollection<T>)b) : new CountingEnumerable<T>(b);
            var comparer = new CountingEqualityComparer<T>();
            equals(wrappedA, wrappedB, comparer);

            // capture timing stats
            const int Trials = 1000;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var originalThreadPriority = Thread.CurrentThread.Priority;
            try
            {
                Thread.CurrentThread.Priority = ThreadPriority.Highest;

                var stopwatch = Stopwatch.StartNew();
                for (var i = 0; i < Trials; ++i)
                {
                    equals(a, b, EqualityComparer<T>.Default);
                }

                return new ProfilingResult
                {
                    Duration = stopwatch.Elapsed,
                    EnumerateCount = wrappedA.EnumerateCount + wrappedB.EnumerateCount,
                    EqualsCount = comparer.EqualsCount,
                    HashCount = comparer.HashCount,
                };
            }
            finally
            {
                Thread.CurrentThread.Priority = originalThreadPriority;
            }
        }

        private class ComparisonResult
        {
            public ProfilingResult CollectionEqualsResult { get; set; }
            public ProfilingResult DictionaryMethodResult { get; set; }
            public ProfilingResult SortMethodResult { get; set; }
        }

        private class ProfilingResult
        {
            public TimeSpan Duration { get; set; }
            public long EnumerateCount { get; set; }
            public long EqualsCount { get; set; }
            public long HashCount { get; set; }

            public override string ToString() => $"Duration={this.Duration}, Enumerate={this.EnumerateCount}, Equals={this.EqualsCount}, Hash={this.HashCount}";

            public void AssertBetterThan(ProfilingResult that)
            {
                var durationScore = (this.Duration - that.Duration).Duration() < TimeSpan.FromMilliseconds(this.Duration.TotalMilliseconds / 200) 
                    ? 0
                    : this.Duration.CompareTo(that.Duration);

                var enumerateScore = this.EnumerateCount.CompareTo(that.EnumerateCount);
                // allow equals to vary by 1 because of the sequence equal optimization
                var equalsScore = Math.Abs(this.EqualsCount - that.EqualsCount) > 1 ? this.EqualsCount.CompareTo(that.EqualsCount) : 0;
                var hashScore = this.HashCount.CompareTo(that.HashCount);

                var scores = new[] { durationScore, enumerateScore, equalsScore, hashScore };
                Assert.True(scores.All(i => i <= 0), "Scores: " + string.Join(", ", scores));
                Assert.True(scores.Any(i => i < 0), "Scores: " + string.Join(", ", scores));
            }
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
        #endregion

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
