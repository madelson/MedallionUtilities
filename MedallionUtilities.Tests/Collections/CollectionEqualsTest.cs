using Medallion.Random;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

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

        // todo more tests to get 100% coverage using ThrowsAt

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

        private static IEnumerable<T> Sequence<T>(params T[] items) { return items.Where(i => true); }

        private static IEnumerable<T> ThrowsAt<T>(IEnumerable<T> items, int count)
        {
            var remaining = count;
            foreach (var item in items)
            {
                if (remaining == 0)
                {
                    Assert.False(true, "ThrowsAt failure!");
                }
                yield return item;
                --remaining;
            }
        }
    }
}
