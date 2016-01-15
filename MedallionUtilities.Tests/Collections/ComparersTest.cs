using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Medallion.Collections
{
    public class ComparersTest
    {
        [Fact]
        public void TestCreate()
        {
            var allEqual = Comparers.Create((int? i) => 0);
            allEqual.Compare(1, 5).ShouldEqual(0);
            Math.Sign(allEqual.Compare(null, 5)).ShouldEqual(-1);
            Math.Sign(allEqual.Compare(5, null)).ShouldEqual(1);

            var evenNumbersFirst = Comparers.Create((int i) => i % 2);
            Enumerable.Range(0, 10).OrderBy(i => i, evenNumbersFirst)
                .SequenceShouldEqual(new[] { 0, 2, 4, 6, 8, 1, 3, 5, 7, 9 });
        }

        [Fact]
        public void TestReverse()
        {
            Enumerable.Range(0, 5).OrderBy(i => i, Comparers.Reverse<int>())
                .SequenceShouldEqual(new[] { 4, 3, 2, 1, 0 });

            Assert.Same(Comparers.Reverse<int>(), Comparers.Reverse<int>());
            Assert.Same(Comparers.Reverse<int>(), Comparer<int>.Default.Reverse());

            var backwardsStringComparer = Comparer<string>.Create((s1, s2) => string.CompareOrdinal(string.Join(string.Empty, s1.Reverse()), string.Join(string.Empty, s2.Reverse())));
            var reverseBackwardsStringComparer = backwardsStringComparer.Reverse();

            new[] { "abc", "cab", "bac" }.OrderBy(s => s, reverseBackwardsStringComparer)
                .SequenceShouldEqual(new[] { "abc", "bac", "cab" });

            Assert.Throws<ArgumentNullException>(() => reverseBackwardsStringComparer.Compare("a", null));
            Math.Sign(Comparer<int?>.Default.Reverse().Compare(1, null)).ShouldEqual(-1);
        }

        [Fact]
        public void TestThenBy()
        {
            var sequence = new[] { new Point(1, 2), new Point(1, 1), new Point(-1, 2), new Point(-1, 1) };
            sequence.OrderBy(t => t, Comparers.Create((Point p) => p.X).Reverse().ThenBy(Comparers.Create((Point p) => p.Y)))
                .SequenceShouldEqual(sequence.OrderByDescending(t => t.X).ThenBy(t => t.Y));
        }

        [Fact]
        public void TestSequenceComparer()
        {
            Assert.Same(Comparers.GetSequenceComparer<int>(), Comparers.GetSequenceComparer<int>());
            Assert.Same(Comparers.GetSequenceComparer<int>(), Comparers.GetSequenceComparer(Comparer<int>.Default));

            var sequence = new[] { new[] { 1, 2, 3 }, new[] { 1, 1, 5 }, new[] { 1, 2, 1 } };
            sequence.OrderBy(i => i, Comparers.GetSequenceComparer<int>())
                .Select(a => string.Join(",", a))
                .SequenceShouldEqual(new[] { "1,1,5", "1,2,1", "1,2,3" });

            sequence.OrderBy(i => i, Comparers.GetSequenceComparer(Comparer<int>.Default.Reverse()))
                .Select(a => string.Join(",", a))
                .SequenceShouldEqual(new[] { "1,2,3", "1,2,1", "1,1,5" });

            Math.Sign(Comparers.GetSequenceComparer<int>().Compare(Empty.Array<int>(), new[] { 1 }))
                .ShouldEqual(-1);
        }

        [Fact]
        public void TestComparerEquality()
        {
            Func<int, int> f = i => -i;
            TestEquality(Comparers.Create(f), Comparers.Create(f), Comparers.Create((int i) => i.ToString()));

            TestEquality(Comparers.Reverse<int>(), Comparers.Reverse<int>(), Comparers.Create((int i) => i.ToString()).Reverse());

            var first = Comparers.Create((string s) => s.Length);
            var second = Comparers.Create((string s) => s[0]);
            TestEquality(first.ThenBy(second), first.ThenBy(second), second.ThenBy(first));

            TestEquality(Comparers.GetSequenceComparer<string>(), Comparers.GetSequenceComparer(Comparer<string>.Default), Comparers.GetSequenceComparer(StringComparer.OrdinalIgnoreCase));
        }

        public static void TestEquality(object obj, object equal, object notEqual)
        {
            Equals(obj, equal).ShouldEqual(true);
            Equals(obj, notEqual).ShouldEqual(false);
            obj.GetHashCode().ShouldEqual(equal.GetHashCode());
            Assert.NotEqual(obj.GetHashCode(), notEqual.GetHashCode());
        }
    }
}
