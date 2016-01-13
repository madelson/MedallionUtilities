using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Medallion.Collections
{
    public class EnumerablesTest
    {
        [Fact]
        public void TestPartition()
        {
            Assert.Throws<ArgumentNullException>(() => default(IEnumerable<object>).Partition(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new[] { 1 }.Partition(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new[] { 1 }.Partition(-3));

            "abcdefg".Partition(3).ElementAt(1)
                .SequenceShouldEqual("def");

            "abcdefg".Partition(3).ElementAt(2)
                .SequenceShouldEqual("g");

            "123456".Partition(2).Count().ShouldEqual(3);

            Enumerable.Empty<string>().Partition(int.MaxValue)
                .SequenceShouldEqual(Enumerable.Empty<List<string>>());

            "1".Partition(int.MaxValue).Single().SequenceEqual("1").ShouldEqual(true);
        }

        [Fact]
        public void TestMinByAndMaxBy()
        {
            Assert.Throws<ArgumentNullException>(() => default(IEnumerable<object>).MinBy(x => 2));
            Assert.Throws<ArgumentNullException>(() => default(IEnumerable<object>).MaxBy(x => 2));
            Assert.Throws<ArgumentNullException>(() => new[] { 1 }.MinBy(default(Func<int, int>)));
            Assert.Throws<ArgumentNullException>(() => new[] { 1 }.MaxBy(default(Func<int, int>)));

            Assert.Throws<InvalidOperationException>(() => new int[0].MinBy(i => "a"));
            Assert.Throws<InvalidOperationException>(() => new int[0].MaxBy(i => "a"));
            new int?[0].MinBy(i => "a").ShouldEqual(null);
            new int?[0].MaxBy(i => "a").ShouldEqual(null);
            new string[0].MinBy(i => 1).ShouldEqual(null);
            new string[0].MaxBy(i => 1).ShouldEqual(null);

            new[] { "z", "aa", }.MinBy(s => s.Length).ShouldEqual("z");
            new[] { "z", "aa" }.MaxBy(s => s.Length).ShouldEqual("aa");
            new[] { default(int?), 1, default(int?) }.MinBy(i => i).ShouldEqual(1);
            new[] { default(int?), 1, default(int?) }.MaxBy(i => i).ShouldEqual(1);

            new[] { 1, 2, 3, 2, 1 }.MinBy(i => 2 * i, Comparers.Reverse<int>()).ShouldEqual(3);
            new[] { 3, 2, 1, 2, 3 }.MaxBy(i => 2 * i, Comparers.Reverse<int>()).ShouldEqual(1);
        }

        [Fact]
        public void TestHasAtLeastAndHasAtMost()
        {
            var lazySequence = Enumerable.Range(0, 10);
            var staticSequence = lazySequence.ToArray();

            Assert.Throws<ArgumentNullException>(() => default(IEnumerable<int>).HasAtLeast(1));
            Assert.Throws<ArgumentNullException>(() => default(IEnumerable<int>).HasAtMost(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => lazySequence.HasAtLeast(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => lazySequence.HasAtMost(-1));

            lazySequence.HasAtLeast(0).ShouldEqual(true);
            staticSequence.HasAtLeast(0).ShouldEqual(true);
            lazySequence.HasAtLeast(10).ShouldEqual(true);
            staticSequence.HasAtLeast(10).ShouldEqual(true);
            lazySequence.HasAtLeast(11).ShouldEqual(false);
            staticSequence.HasAtLeast(11).ShouldEqual(false);
            Assert.Throws<ArithmeticException>(() => lazySequence.HasAtLeast(int.MaxValue));
            staticSequence.HasAtLeast(int.MaxValue).ShouldEqual(false);

            lazySequence.HasAtMost(0).ShouldEqual(false);
            staticSequence.HasAtMost(0).ShouldEqual(false);
            lazySequence.HasAtMost(10).ShouldEqual(true);
            staticSequence.HasAtMost(10).ShouldEqual(true);
            lazySequence.HasAtMost(11).ShouldEqual(true);
            staticSequence.HasAtMost(11).ShouldEqual(true);
            lazySequence.HasAtMost(int.MaxValue).ShouldEqual(true);
            staticSequence.HasAtMost(int.MaxValue).ShouldEqual(true);
        }
    }
}
