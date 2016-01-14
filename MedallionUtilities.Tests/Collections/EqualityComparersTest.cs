using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Medallion.Collections
{
    public class EqualityComparersTest
    {
        [Fact]
        public void TestCreate()
        {
            Assert.Throws<ArgumentNullException>(() => EqualityComparers.Create<int>(null));

            var comparer = EqualityComparers.Create<string>((a, b) => a.Length == b.Length);
            comparer.Equals("a", "b").ShouldEqual(true);
            comparer.Equals(null, string.Empty).ShouldEqual(false);
            comparer.Equals(string.Empty, null).ShouldEqual(false);
            comparer.Equals("aa", "b").ShouldEqual(false);
            comparer.GetHashCode("abc").ShouldEqual(-1);

            comparer = EqualityComparers.Create<string>((a, b) => a.Length == b.Length, s => s.Length);
            comparer.Equals("a", "b").ShouldEqual(true);
            comparer.Equals(null, string.Empty).ShouldEqual(false);
            comparer.Equals(string.Empty, null).ShouldEqual(false);
            comparer.Equals("aa", "b").ShouldEqual(false);
            comparer.GetHashCode("abc").ShouldEqual(3);
        }

        [Fact]
        public void TestCreateByKey()
        {
            var comparer = EqualityComparers.Create((string s) => s.Length);
            comparer.Equals("a", "b").ShouldEqual(true);
            comparer.Equals(null, string.Empty).ShouldEqual(false);
            comparer.Equals(string.Empty, null).ShouldEqual(false);
            comparer.Equals("aa", "b").ShouldEqual(false);
            comparer.GetHashCode("abc").ShouldEqual(3);
        }

        [Fact]
        public void TestReferenceComparer()
        {
            var referenceComparer = EqualityComparers.GetReferenceComparer<string>();
            Assert.Same(referenceComparer, EqualityComparers.GetReferenceComparer<string>());

            referenceComparer.Equals(new string('a', 1), new string('a', 1)).ShouldEqual(false);
            var text = new string('a', 1);
            referenceComparer.Equals(text, text).ShouldEqual(true);
            referenceComparer.GetHashCode(text).ShouldEqual(referenceComparer.GetHashCode(text));
            referenceComparer.Equals(text, null).ShouldEqual(false);
            referenceComparer.Equals(null, null).ShouldEqual(true);

            var obj = new object();
            EqualityComparers.GetReferenceComparer<object>().GetHashCode(obj)
                .ShouldEqual(obj.GetHashCode());
        }

        [Fact]
        public void TestCollectionComparer()
        {
            var a = new[] { 1, 2, 3 };
            var b = new[] { 3, 2, 1 };
            var c = new[] { 1, 2, 4 };

            var comparer = EqualityComparers.GetCollectionComparer<int>();
            Assert.Same(comparer, EqualityComparers.GetCollectionComparer<int>());
            Assert.Same(comparer, EqualityComparers.GetCollectionComparer(EqualityComparer<int>.Default));
            comparer.Equals(a, b).ShouldEqual(true);
            comparer.Equals(a, null).ShouldEqual(false);
            comparer.Equals(a, c).ShouldEqual(false);
            comparer.GetHashCode(a).ShouldEqual(comparer.GetHashCode(b));
            Assert.NotEqual(comparer.GetHashCode(a), comparer.GetHashCode(c));

            var stringComparer = EqualityComparers.GetCollectionComparer(StringComparer.OrdinalIgnoreCase);
            var aa = new[] { "a", "B", "C" };
            var bb = new[] { "B", "A", "c" };
            var cc = new[] { "a", "B", "C", "d" };
            stringComparer.Equals(aa, bb).ShouldEqual(true);
            stringComparer.Equals(aa, cc).ShouldEqual(false);
            stringComparer.GetHashCode(aa).ShouldEqual(stringComparer.GetHashCode(bb));
            Assert.NotEqual(stringComparer.GetHashCode(aa), stringComparer.GetHashCode(cc));
        }

        [Fact]
        public void TestSequenceComparer()
        {
            var a = new[] { 1, 2, 3 };
            var b = new[] { 1, 2, 3 };
            var c = new[] { 1, 3, 2 };

            var comparer = EqualityComparers.GetSequenceComparer<int>();
            Assert.Same(comparer, EqualityComparers.GetSequenceComparer<int>());
            Assert.Same(comparer, EqualityComparers.GetSequenceComparer(EqualityComparer<int>.Default));

            comparer.Equals(a, b).ShouldEqual(true);
            comparer.Equals(a, c).ShouldEqual(false);
            comparer.Equals(null, a).ShouldEqual(false);
            comparer.Equals(null, null).ShouldEqual(true);
            comparer.GetHashCode(null).ShouldEqual(((IEqualityComparer)comparer).GetHashCode(null));
            comparer.GetHashCode(a).ShouldEqual(comparer.GetHashCode(b));
            Assert.NotEqual(comparer.GetHashCode(a), comparer.GetHashCode(c));

            var stringComparer = EqualityComparers.GetSequenceComparer(StringComparer.OrdinalIgnoreCase);
            var aa = new[] { "a", "B", "C" };
            var bb = new[] { "A", "b", "c" };
            var cc = new[] { "a", "B", "C", "d" };
            stringComparer.Equals(aa, bb).ShouldEqual(true);
            stringComparer.Equals(aa, cc).ShouldEqual(false);
            stringComparer.GetHashCode(aa).ShouldEqual(stringComparer.GetHashCode(bb));
            Assert.NotEqual(stringComparer.GetHashCode(aa), stringComparer.GetHashCode(cc));
        }
    }
}
