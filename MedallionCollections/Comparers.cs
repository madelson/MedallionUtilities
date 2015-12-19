using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.Collections
{
    public static class Comparers
    {
        #region ---- Key Comparer ----
        public static Comparer<T> Create<T, TKey>(Func<T, TKey> keySelector, IComparer<TKey> keyComparer = null)
        {
            Throw.IfNull(keySelector, nameof(keySelector));

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
        public static IComparer<T> Reverse<T>()
        {
            return ReverseComparer<T>.Default;
        }

        public static IComparer<T> Reverse<T>(this IComparer<T> comparer)
        {
            Throw.IfNull(comparer, nameof(comparer));

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
        public static Comparer<T> ThenBy<T>(this IComparer<T> first, IComparer<T> second)
        {
            Throw.IfNull(first, nameof(first));
            Throw.IfNull(second, nameof(second));

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
    }
}
