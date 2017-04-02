using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Medallion
{
    public static class TestHelper
    {
        public static T ShouldEqual<T>(this T actual, T expected, IEqualityComparer<T> comparer = null, int? precision = null)
        {
            if (precision.HasValue)
            {
                if (comparer != null) { throw new ArgumentException("comparer and precision cannot be specified together"); }
                Assert.Equal(actual: (dynamic)actual, expected: (dynamic)expected, precision: precision.Value);
            }
            else
            {
                Assert.Equal(actual: actual, expected: expected, comparer: comparer ?? EqualityComparer<T>.Default);
            }

            return actual;
        }

        public static IEnumerable<T> SequenceShouldEqual<T>(this IEnumerable<T> actual, IEnumerable<T> expected, IEqualityComparer<T> comparer = null)
        {
            Assert.True(actual.SequenceEqual(expected, comparer ?? EqualityComparer<T>.Default));
            return actual;
        }
    }
}
