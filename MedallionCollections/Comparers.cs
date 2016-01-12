using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.Collections
{
    /// <summary>
    /// Contains utilities for creating and working with <see cref="IComparer{T}"/>s
    /// </summary>
    public static class Comparers
    {
        #region ---- Key Comparer ----
        /// <summary>
        /// Creates a <see cref="Comparer{T}"/> which compares values of type <typeparamref name="T"/> by
        /// projecting them to type <typeparamref name="TKey"/> using the given <paramref name="keySelector"/>.
        /// The optional <paramref name="keyComparer"/> determines how keys are compared
        /// </summary>
        public static Comparer<T> Create<T, TKey>(Func<T, TKey> keySelector, IComparer<TKey> keyComparer = null)
        {
            if (keySelector == null) { throw new ArgumentNullException(nameof(keySelector)); }

            return new KeyComparer<T, TKey>(keySelector, keyComparer ?? Comparer<TKey>.Default);
        }

        private sealed class KeyComparer<T, TKey> : Comparer<T>
        {
            private readonly Func<T, TKey> keySelector;
            private readonly IComparer<TKey> keyComparer;

            public KeyComparer(Func<T, TKey> keySelector, IComparer<TKey> keyComparer)
            {
                this.keySelector = keySelector;
                this.keyComparer = keyComparer;
            }

            public override int Compare(T x, T y)
            {
                // from Comparer<T>.Compare(object, object)
                if (x == null)
                {
                    return y == null ? 0 : -1;
                }
                if (y == null)
                {
                    return 1;
                }

                return this.keyComparer.Compare(this.keySelector(x), this.keySelector(y));
            }
        }
        #endregion

        #region ---- Reverse ----
        /// <summary>
        /// Gets an <see cref="IComparer{T}"/> which represents the reverse of
        /// the order implied by <see cref="Comparer{T}.Default"/>
        /// </summary>
        public static IComparer<T> Reverse<T>()
        {
            return ReverseComparer<T>.Default;
        }

        /// <summary>
        /// Gets an <see cref="IComparer{T}"/> which represents the reverse of
        /// the order implied by the given <paramref name="comparer"/>
        /// </summary>
        public static IComparer<T> Reverse<T>(this IComparer<T> comparer)
        {
            if (comparer == null) { throw new ArgumentNullException(nameof(comparer)); }

            return comparer == Comparer<T>.Default
                ? Reverse<T>()
                : new ReverseComparer<T>(comparer);
        }

        // we don't want Comparer<T> here because that doesn't let us override
        // the comparison of nulls in the Compare(object, object) method
        private sealed class ReverseComparer<T> : IComparer<T>, IComparer
        {
            public static readonly ReverseComparer<T> Default = new ReverseComparer<T>(Comparer<T>.Default);

            private readonly IComparer<T> comparer;

            public ReverseComparer(IComparer<T> comparer)
            {
                this.comparer = comparer;
            }

            public int Compare(T x, T y)
            {
                return this.comparer.Compare(y, x);
            }

            int IComparer.Compare(object x, object y)
            {
                return this.Compare((T)x, (T)y);
            }
        }
        #endregion

        #region ---- ThenBy ----
        /// <summary>
        /// Gets a <see cref="Comparer{T}"/> which compares using <paramref name="first"/>
        /// and breaks ties with <paramref name="second"/>
        /// </summary>
        public static Comparer<T> ThenBy<T>(this IComparer<T> first, IComparer<T> second)
        {
            if (first == null) { throw new ArgumentNullException(nameof(first)); }
            if (second == null) { throw new ArgumentNullException(nameof(second)); }

            return new ThenByComparer<T>(first, second);
        }

        private sealed class ThenByComparer<T> : Comparer<T>
        {
            private readonly IComparer<T> first, second;

            public ThenByComparer(IComparer<T> first, IComparer<T> second)
            {
                this.first = first;
                this.second = second;
            }

            public override int Compare(T x, T y)
            {
                var firstComparison = this.first.Compare(x, y);
                return firstComparison != 0 ? firstComparison : this.second.Compare(x, y);
            }
        }
        #endregion

        #region ---- Sequence Comparer ----
        /// <summary>
        /// Gets a <see cref="Comparer{T}"/> which sorts sequences lexographically. The optional
        /// <paramref name="elementComparer"/> can be used to override comparisons of individual elements
        /// </summary>
        public static Comparer<IEnumerable<T>> GetSequenceComparer<T>(IComparer<T> elementComparer = null)
        {
            return elementComparer == null || elementComparer == Comparer<T>.Default
                ? SequenceComparer<T>.DefaultInstance
                : new SequenceComparer<T>(elementComparer);
        }

        private sealed class SequenceComparer<T> : Comparer<IEnumerable<T>>
        {
            private static Comparer<IEnumerable<T>> defaultInstance;
            public static Comparer<IEnumerable<T>> DefaultInstance
                => defaultInstance ?? (defaultInstance = new SequenceComparer<T>(Comparer<T>.Default));

            private readonly IComparer<T> elementComparer;

            public SequenceComparer(IComparer<T> elementComparer)
            {
                this.elementComparer = elementComparer;
            }

            public override int Compare(IEnumerable<T> x, IEnumerable<T> y)
            {
                // from Comparer<T>.Compare(object, object)
                if (x == null)
                {
                    return y == null ? 0 : -1;
                }
                if (y == null)
                {
                    return 1;
                }
               
                if (ReferenceEquals(x, y))
                {
                    return 0;
                }

                using (var xEnumerator = x.GetEnumerator())
                using (var yEnumerator = y.GetEnumerator())
                {
                    while (true)
                    {
                        var xHasMore = xEnumerator.MoveNext();
                        var yHasMore = yEnumerator.MoveNext();
                        
                        if (!xHasMore)
                        {
                            return yHasMore ? -1 : 0;
                        }
                        if (!yHasMore)
                        {
                            return 1;
                        }

                        var cmp = this.elementComparer.Compare(xEnumerator.Current, yEnumerator.Current);
                        if (cmp != 0)
                        {
                            return cmp;
                        }
                    }
                }
            }
        }
        #endregion
    }
}
