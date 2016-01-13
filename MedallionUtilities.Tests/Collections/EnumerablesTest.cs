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
    }
}
