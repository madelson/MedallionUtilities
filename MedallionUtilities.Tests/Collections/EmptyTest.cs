using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Medallion.Collections
{
    public class EmptyTest
    {
        private readonly ITestOutputHelper output;

        public EmptyTest(ITestOutputHelper output) { this.output = output; }

        [Fact]
        public void TestEnumerationSpeed()
        {
            IList<int> list = new List<int>();
            var emptyList = Empty.List<int>();

            // warmup
            list.Select(i => i).ToArray()
                .SequenceShouldEqual(emptyList.Select(i => i).ToArray());

            const int trials = 10000000;
            long sum;

            var stopwatch = Stopwatch.StartNew();

            sum = 0L;
            for (var i = 0; i < trials; ++i)
            {
                foreach (var item in list)
                {
                    sum += item;
                }
            }

            var listTiming = stopwatch.Elapsed;
            sum.ShouldEqual(0);

            stopwatch.Restart();

            sum = 0L;
            for (var i = 0; i < trials; ++i)
            {
                foreach (var item in emptyList)
                {
                    sum += item;
                }
            }

            var emptyListTiming = stopwatch.Elapsed;
            sum.ShouldEqual(0);

            this.output.WriteLine($"{listTiming} vs. {emptyListTiming}");
            (emptyListTiming < listTiming).ShouldEqual(true);
        }

        [Fact]
        public void BasicChecks()
        {
            Empty.Array<int>().Length.ShouldEqual(0);

            Assert.Throws<NotSupportedException>(() => Empty.Dictionary<int, string>().Add(1, "1"));

            Empty.Set<string>().Contains("b").ShouldEqual(false);
        }
    }
}
