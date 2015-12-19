using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.Collections
{
    public static class EqualityComparers
    {
        #region ---- Func Comparer ----
        public static EqualityComparer<T> Create<T>(Func<T, T, bool> equals, Func<T, int> hash = null)
        {
            Throw.IfNull(equals, "equals");

            return new FuncEqualityComparer<T>(equals, hash);
        }

        private sealed class FuncEqualityComparer<T> : EqualityComparer<T>
        {
            private readonly Func<T, T, bool> equals;
            private readonly Func<T, int> hash;

            public FuncEqualityComparer(Func<T, T, bool> equals, Func<T, int> hash)
            {
                this.equals = equals;
                this.hash = hash;
            }

            public override bool Equals(T x, T y)
            {
                // null checks consistent with Equals(object, object)
                return x == null
                    ? y == null
                    : y != null && this.equals(x, y);
            }

            public override int GetHashCode(T obj)
            {
                return obj == null ? 0 // consistent with GetHashCode(object)
                    : this.hash == null ? -1
                    : this.hash(obj);
            }
        }
        #endregion

        #region ---- Key Comparer ----
        public static EqualityComparer<T> Create<T, TKey>(Func<T, TKey> keySelector, IEqualityComparer<TKey> keyComparer = null)
        {
            Throw.IfNull(keySelector, "keySelector");

            var keyComparerToUse = keyComparer ?? EqualityComparer<TKey>.Default;
            return new FuncEqualityComparer<T>(
                equals: (@this, that) => keyComparerToUse.Equals(keySelector(@this), keySelector(that)),
                hash: obj => keyComparerToUse.GetHashCode(keySelector(obj))
            );
        }
        #endregion

        #region ---- Reference Comparer ----
        public static EqualityComparer<T> GetReferenceComparer<T>()
            where T : class
        {
            return ReferenceEqualityComparer<T>.Instance;
        }

        private sealed class ReferenceEqualityComparer<T> : EqualityComparer<T>
            where T : class
        {
            public static readonly EqualityComparer<T> Instance = new ReferenceEqualityComparer<T>();

            public override bool Equals(T x, T y)
            {
                return ReferenceEquals(x, y);
            }

            public override int GetHashCode(T obj)
            {
                // handles nulls
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
        #endregion

        #region ---- Collection Comparer ----
        public static EqualityComparer<IEnumerable<TElement>> GetCollectionComparer<TElement>(IEqualityComparer<TElement> elementComparer = null)
        {
            return elementComparer == null || elementComparer == EqualityComparer<TElement>.Default
                ? CollectionComparer<TElement>.DefaultInstance
                : new CollectionComparer<TElement>(elementComparer);
        }

        private sealed class CollectionComparer<TElement> : EqualityComparer<IEnumerable<TElement>>
        {
            private static EqualityComparer<IEnumerable<TElement>> defaultInstance;
            public static EqualityComparer<IEnumerable<TElement>> DefaultInstance
            {
                get
                {
                    return defaultInstance ?? (defaultInstance = new CollectionComparer<TElement>(EqualityComparer<TElement>.Default));
                }
            }

            private readonly IEqualityComparer<TElement> elementComparer;

            public CollectionComparer(IEqualityComparer<TElement> elementComparer)
            {
                this.elementComparer = elementComparer;
            }

            public override bool Equals(IEnumerable<TElement> x, IEnumerable<TElement> y)
            {
                if (x == null)
                {
                    return y == null;
                }
                else if (y == null)
                {
                    return false;
                }

                throw new NotImplementedException();
                //return x.CollectionEquals(y, this.elementComparer);
            }

            public override int GetHashCode(IEnumerable<TElement> obj)
            {
                return obj != null
                    ? obj.Aggregate(-1, (hash, element) => hash ^ this.elementComparer.GetHashCode(element))
                    : 0;
            }
        }
        #endregion

        #region ---- Sequence Comparer ----
        public static EqualityComparer<IEnumerable<TElement>> GetSequenceComparer<TElement>(IEqualityComparer<TElement> elementComparer = null)
        {
            return elementComparer == null || elementComparer == EqualityComparer<TElement>.Default
                ? SequenceComparer<TElement>.DefaultInstance
                : new SequenceComparer<TElement>(elementComparer);
        }

        private sealed class SequenceComparer<TElement> : EqualityComparer<IEnumerable<TElement>>
        {
            private static EqualityComparer<IEnumerable<TElement>> defaultInstance;
            public static EqualityComparer<IEnumerable<TElement>> DefaultInstance
            {
                get
                {
                    return defaultInstance ?? (defaultInstance = new SequenceComparer<TElement>(EqualityComparer<TElement>.Default));
                }
            }

            private readonly IEqualityComparer<TElement> elementComparer;

            public SequenceComparer(IEqualityComparer<TElement> elementComparer)
            {
                this.elementComparer = elementComparer;
            }

            public override bool Equals(IEnumerable<TElement> x, IEnumerable<TElement> y)
            {
                if (x == null)
                {
                    return y == null;
                }
                if (y == null)
                {
                    return false;
                }

                return x.SequenceEqual(y, this.elementComparer);
            }

            public override int GetHashCode(IEnumerable<TElement> obj)
            {
                return obj != null  
                    // hash combine logic based on .NET Tuple.CombineHashCodes
                    ? obj.Aggregate(-1, (hash, element) => (((hash << 5) + hash) ^ this.elementComparer.GetHashCode(element)))
                    : 0;
            }
        }
        #endregion
    }
}
