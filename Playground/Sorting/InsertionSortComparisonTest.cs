using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Playground.Sorting
{
    public class InsertionSortComparisonTest
    {
        private ITestOutputHelper output;

        public InsertionSortComparisonTest(ITestOutputHelper output)
        {
            this.output = output;
        }

        //[Fact]
        //public void StringSortTest()
        //{
        //    var strings = Enumerable.Range(0, 10000).Select(_ => Guid.NewGuid().ToString())
        //        .ToArray();
        //    string[] stringsClone = null;

        //    var a = PerformanceTester.Run(
        //        () => InsertionSort.Sort<string>(stringsClone, 0, stringsClone.Length - 1, StringComparer.Ordinal),
        //        () => stringsClone = (string[])strings.Clone(),
        //        TimeSpan.FromSeconds(5)
        //    );

        //    var b = PerformanceTester.Run(
        //        () => InsertionSort.Sort(stringsClone, 0, stringsClone.Length - 1, new StructStringComparer()),
        //        () => stringsClone = (string[])strings.Clone(),
        //        TimeSpan.FromSeconds(5)
        //    );

        //    var c = PerformanceTester.Run(
        //        () => InsertionSort.Sort<string>(stringsClone, 0, stringsClone.Length - 1, new StructStringComparer()),
        //        () => stringsClone = (string[])strings.Clone(),
        //        TimeSpan.FromSeconds(5)
        //    );

        //    this.output.WriteLine(a);
        //    this.output.WriteLine(b);
        //    this.output.WriteLine(c);
        //}

        private struct StructStringComparer : IComparer<string>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Compare(string x, string y) => string.CompareOrdinal(x, y);
        }
    }

    public static class PerformanceTester 
    {
        public static string Run(Action action, Action prepare, TimeSpan maxTime)
        {
            prepare();
            action();

            GC.Collect();
            GC.WaitForPendingFinalizers();

            var originalPriority = Thread.CurrentThread.Priority;
            try
            {
                Thread.CurrentThread.Priority = ThreadPriority.Highest;

                var stopwatch = Stopwatch.StartNew();
                var count = 0L;
                var total = TimeSpan.Zero;
                do
                {
                    prepare();
                    var before = stopwatch.Elapsed;
                    action();
                    var after = stopwatch.Elapsed;
                    ++count;
                    total += after - before;
                }
                while (stopwatch.Elapsed < maxTime);

                return $"{count} runs in {total}: avg of {TimeSpan.FromTicks(total.Ticks / count)}";
            }
            finally
            {
                Thread.CurrentThread.Priority = originalPriority;
            }
        }
    }
}
