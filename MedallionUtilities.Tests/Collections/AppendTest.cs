using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Medallion.Collections
{
    public sealed class AppendTest
    {
        private readonly ITestOutputHelper output;

        public AppendTest(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void BasicTest()
        {
            var sequence = "a".Append('b')
                .Append("d".Prepend("c"))
                .Prepend('0')
                .Append("h".Prepend('g').Append('i').Prepend("ef").Append("jk"));
            string.Join(string.Empty, sequence).ShouldEqual("0abcdefghijk");
        }

        // tests a bug with appending one element to a list built by other means
        [Fact]
        public void TestConcatAppend()
        {
            var list = new List<object>().Concat(ImmutableList.Create("a"));
            list.Append("b").SequenceShouldEqual(new[] { "a", "b" });
            list.Prepend("b").SequenceShouldEqual(new[] { "b", "a" });
        }

        [Fact]
        public void PerformanceTest()
        {
            // warmup
            Enumerable.Concat("abc", "def").ToArray();
            "a".Append('b').Append("c").Prepend('d').Prepend("f").ToArray();

            const int Length = 10000;
            var stopwatch = new Stopwatch();

            // concat build
            stopwatch.Restart();
            Enumerable.Range(0, Length).Aggregate(Enumerable.Empty<int>(), (e, i) => e.Concat(new[] { i }))
                .Count();
            var concatBuildTime = stopwatch.Elapsed;

            // append build
            stopwatch.Restart();
            Enumerable.Range(0, Length).Aggregate(Enumerable.Empty<int>(), (e, i) => e.Append(i))
                .Count();
            var appendBuildTime = stopwatch.Elapsed;

            this.output.WriteLine($"{appendBuildTime}, {concatBuildTime}");
            Assert.True(appendBuildTime < concatBuildTime);
        }
    }
}
