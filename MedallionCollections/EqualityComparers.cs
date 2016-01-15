using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.Collections
{
    /// <summary>
    /// Provides utilities for creating and working with instances of <see cref="IEqualityComparer{T}"/>
    /// </summary>
    public static class EqualityComparers
    {
        #region ---- Func Comparer ----
        /// <summary>
        /// Creates an <see cref="EqualityComparer{T}"/> using the given <paramref name="equals"/> function
        /// for equality and the optional <paramref name="hash"/> function for hashing (if <param name="hash"> is not
        /// provided, all values hash to 0). Note that null values are handled directly by the comparer and will not
        /// be passed to these functions
        /// </summary>
        public static EqualityComparer<T> Create<T>(Func<T, T, bool> equals, Func<T, int> hash = null)
        {
            if (equals == null) { throw new ArgumentNullException(nameof(equals)); }

            return new FuncEqualityComparer<T>(equals, hash);
        }

        private sealed class FuncEqualityComparer<T> : EqualityComparer<T>
        {
            private static readonly Func<T, int> DefaultHash = _ => -1;

            private readonly Func<T, T, bool> equals;
            private readonly Func<T, int> hash;

            public FuncEqualityComparer(Func<T, T, bool> equals, Func<T, int> hash)
            {
                this.equals = equals;
                this.hash = hash ?? DefaultHash;
            }

            public override bool Equals(T x, T y)
            {
                // TODO do these cause boxing?
                // null checks consistent with Equals(object, object)
                return x == null
                    ? y == null
                    : y != null && this.equals(x, y);
            }

            public override int GetHashCode(T obj)
            {
                // consistent with GetHashCode(object)
                return obj == null ? 0 : this.hash(obj);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(obj, this)) { return true; }

                var that = obj as FuncEqualityComparer<T>;
                return that != null
                    && that.equals.Equals(this.equals)
                    && that.hash.Equals(this.hash);
            }

            public override int GetHashCode()
            {
                return unchecked((3 * this.equals.GetHashCode()) + this.hash.GetHashCode());
            }
        }
        #endregion

        #region ---- Key Comparer ----
        /// <summary>
        /// Creates an <see cref="EqualityComparer{T}"/> which compares elements of type <typeparamref name="T"/> by projecting
        /// them to an instance of type <typeparamref name="TKey"/> using the provided <paramref name="keySelector"/> and comparing/hashing
        /// these keys. The optional <param name="keyComparer"/> argument can be used to specify how the keys are compared. Note that null
        /// values are handled directly by the comparer and will not be passed to <paramref name="keySelector"/>
        /// </summary>
        public static EqualityComparer<T> Create<T, TKey>(Func<T, TKey> keySelector, IEqualityComparer<TKey> keyComparer = null)
        {
            if (keySelector == null) { throw new ArgumentNullException(nameof(keySelector)); }

            return new KeyComparer<T, TKey>(keySelector, keyComparer);
        }

        private sealed class KeyComparer<T, TKey> : EqualityComparer<T>
        {
            private readonly Func<T, TKey> keySelector;
            private readonly IEqualityComparer<TKey> keyComparer;

            public KeyComparer(Func<T, TKey> keySelector, IEqualityComparer<TKey> keyComparer)
            {
                this.keySelector = keySelector;
                this.keyComparer = keyComparer ?? EqualityComparer<TKey>.Default;
            }

            public override bool Equals(T x, T y)
            {
                if (x == null) { return y == null; }
                if (y == null) { return false; }

                return this.keyComparer.Equals(this.keySelector(x), this.keySelector(y));
            }

            public override int GetHashCode(T obj)
            {
                return obj == null ? 0 : this.keyComparer.GetHashCode(this.keySelector(obj));
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(obj, this)) { return true; }

                var that = obj as KeyComparer<T, TKey>;
                return that != null
                    && that.keySelector.Equals(this.keySelector)
                    && that.keyComparer.Equals(this.keyComparer);
            }

            public override int GetHashCode()
            {
                return unchecked((3 * this.keySelector.GetHashCode()) + this.keyComparer.GetHashCode());
            }
        }
        #endregion

        #region ---- Reference Comparer ----
        /// <summary>
        /// Gets a cached <see cref="EqualityComparer{T}"/> instance which performs all comparisons by reference
        /// (i. e. as if with <see cref="object.ReferenceEquals(object, object)"/>). Uses 
        /// <see cref="RuntimeHelpers.GetHashCode(object)"/> to emulate the native identity-based hash function
        /// </summary>
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
        /// <summary>
        /// Gets an <see cref="EqualityComparer{T}"/> that compares instances of <see cref="IEnumerable{TElement}"/> as
        /// if with <see cref="CollectionHelper.CollectionEquals{TElement}(IEnumerable{TElement}, IEnumerable{TElement}, IEqualityComparer{TElement})"/>.
        /// The optional <paramref name="elementComparer"/> can be used to override the comparison of individual elements
        /// </summary>
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

                return x.CollectionEquals(y, this.elementComparer);
            }

            public override int GetHashCode(IEnumerable<TElement> obj)
            {
                return obj != null
                    // combine hashcodes with xor to be order-insensitive
                    ? obj.Aggregate(-1, (hash, element) => hash ^ this.elementComparer.GetHashCode(element))
                    : 0;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(obj, this)) { return true; }

                var that = obj as CollectionComparer<TElement>;
                return that != null && that.elementComparer.Equals(this.elementComparer);
            }

            public override int GetHashCode()
            {
                return ReferenceEquals(this, DefaultInstance)
                    ? base.GetHashCode()
                    : unchecked((3 * DefaultInstance.GetHashCode()) + this.elementComparer.GetHashCode());
            }
        }
        #endregion

        #region ---- Sequence Comparer ----
        /// <summary>
        /// Gets an <see cref="EqualityComparer{T}"/> which compares instances of <see cref="IEnumerable{TElement}"/> as if
        /// with <see cref="Enumerable.SequenceEqual{TSource}(IEnumerable{TSource}, IEnumerable{TSource})"/>. The optional 
        /// <paramref name="elementComparer"/> can be used to override the comparison of individual elements
        /// </summary>
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

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(obj, this)) { return true; }

                var that = obj as SequenceComparer<TElement>;
                return that != null && that.elementComparer.Equals(this.elementComparer);
            }

            public override int GetHashCode()
            {
                return ReferenceEquals(this, DefaultInstance)
                    ? base.GetHashCode()
                    : unchecked((3 * DefaultInstance.GetHashCode()) + this.elementComparer.GetHashCode());
            }
        }
        #endregion
    }
}
