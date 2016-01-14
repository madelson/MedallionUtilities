using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Medallion.Collections
{
    public class TraverseTest
    {
        [Fact]
        public void TestAlong()
        {
            Assert.Throws<ArgumentNullException>(() => Traverse.Along("a", null));

            var ex = new Exception("a", new Exception("b", new Exception("c")));

            Traverse.Along(ex, e => e.InnerException)
                .SequenceShouldEqual(new[] { ex, ex.InnerException, ex.InnerException.InnerException });

            Traverse.Along(default(Exception), e => e.InnerException)
                .SequenceShouldEqual(Empty.Enumerable<Exception>());
        }

        [Fact]
        public void TestDepthFirst()
        {
            Assert.Throws<ArgumentNullException>(() => Traverse.DepthFirst("a", null));

            Traverse.DepthFirst("abcd", s => s.Length < 2 ? Empty.Enumerable<string>() : new[] { s.Substring(0, s.Length - 1), s.Substring(1) })
                    .SequenceShouldEqual(new[]
                    {
                        "abcd",
                        "abc",
                        "ab",
                        "a",
                        "b",
                        "bc",
                        "b",
                        "c",
                        "bcd",
                        "bc",
                        "b",
                        "c",
                        "cd",
                        "c",
                        "d"
                    });
        }

        [Fact]
        public void TestBreadthFirst()
        {
            Assert.Throws<ArgumentNullException>(() => Traverse.BreadthFirst("a", null));

            Traverse.BreadthFirst("abcd", s => s.Length < 2 ? Empty.Enumerable<string>() : new[] { s.Substring(0, s.Length - 1), s.Substring(1) })
                    .SequenceShouldEqual(new[]
                    {
                        "abcd",
                        "abc",
                        "bcd",
                        "ab",
                        "bc",
                        "bc",
                        "cd",
                        "a",
                        "b",
                        "b",
                        "c",
                        "b",
                        "c",
                        "c",
                        "d",
                    });
        }

        [Fact]
        public void DepthFirstEnumeratorsAreLazyAndDisposeProperly()
        {
            var helper = new EnumeratorHelper();

            var sequence = Traverse.DepthFirst(10, i => helper.MakeEnumerator(i - 1));

            helper.IterateCount.ShouldEqual(0);
            helper.StartCount.ShouldEqual(0);
            helper.EndCount.ShouldEqual(0);

            using (var enumerator = sequence.GetEnumerator())
            {
                for (var i = 0; i < 10; ++i)
                {
                    enumerator.MoveNext().ShouldEqual(true);
                }
                helper.IterateCount.ShouldEqual(9); // -1 for root
            }

            helper.StartCount.ShouldEqual(helper.EndCount);
        }

        [Fact]
        public void BreadthFirstEnumeratorsAreLazyAndDisposeProperly()
        {
            var helper = new EnumeratorHelper();

            var sequence = Traverse.BreadthFirst(10, i => helper.MakeEnumerator(i - 1));

            helper.IterateCount.ShouldEqual(0);
            helper.StartCount.ShouldEqual(0);
            helper.EndCount.ShouldEqual(0);

            using (var enumerator = sequence.GetEnumerator())
            {
                for (var i = 0; i < 10; ++i)
                {
                    enumerator.MoveNext().ShouldEqual(true);
                }
                helper.IterateCount.ShouldEqual(9); // -1 for root
            }

            helper.StartCount.ShouldEqual(helper.EndCount);
        }

        private class EnumeratorHelper
        {
            public int StartCount { get; private set; }
            public int EndCount { get; private set; }
            public int IterateCount { get; private set; }

            public IEnumerable<int> MakeEnumerator(int i)
            {
                ++this.StartCount;

                try
                {
                    for (var j = 0; j < i; ++j)
                    {
                        ++this.IterateCount;
                        yield return i;
                    }
                }
                finally
                {
                    ++this.EndCount;
                }
            }
        }
    }
}
