using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.Collections
{
    /// <summary>
    /// Provides access to cached immutable instances of empty collections implementing various
    /// interfaces. The collections provided by this class are optimized empty implementations, and
    /// do no work when methods are called. Similarly, the can be enumerated without allocation
    /// </summary>
    public static class Empty
    {
        /// <summary>A cached instance of <see cref="IEnumerable"/></summary>
        public static IEnumerable ObjectEnumerable => EmptyCollection<object>.Instance;

        /// <summary>A cached readonly instance of <see cref="ICollection"/></summary>
        public static ICollection ObjectCollection => EmptyCollection<object>.Instance;
        
        /// <summary>A cached readonly instance of <see cref="IList"/></summary>
        public static IList ObjectList => EmptyCollection<object>.Instance;
        
        /// <summary>A cached readonly instance of <see cref="IDictionary"/></summary>
        public static IDictionary ObjectDictionary => EmptyDictionary<object, object>.Instance;

        /// <summary>A cached instance of <see cref="IEnumerable{T}"/></summary>
        public static IEnumerable<T> Enumerable<T>() => EmptyCollection<T>.Instance;

        /// <summary>A cached readonly instance of <see cref="ICollection{T}"/></summary>
        public static ICollection<T> Collection<T>() => EmptyCollection<T>.Instance;
        
        /// <summary>A cached instance of <see cref="IReadOnlyCollection{T}"/></summary>
        public static IReadOnlyCollection<T> ReadOnlyCollection<T>() => EmptyCollection<T>.Instance;
        
        /// <summary>A cached instance of an array of <typeparamref name="T"/></summary>
        public static T[] Array<T>() => EmptyArray<T>.Instance;

        /// <summary>A cached readonly instance of <see cref="IList{T}"/></summary>
        public static IList<T> List<T>() => EmptyCollection<T>.Instance;
        
        /// <summary>A cached instance of <see cref="IReadOnlyList{T}"/></summary>
        public static IReadOnlyList<T> ReadOnlyList<T>() => EmptyCollection<T>.Instance;

        /// <summary>A cached readonly instance of <see cref="ISet{T}"/></summary>
        public static ISet<T> Set<T>() => EmptyCollection<T>.Instance;

        /// <summary>A cached readonly instance of <see cref="IDictionary{TKey, TValue}"/></summary>
        public static IDictionary<TKey, TValue> Dictionary<TKey, TValue>() => EmptyDictionary<TKey, TValue>.Instance;
        
        /// <summary>A cached instance of <see cref="IReadOnlyDictionary{TKey, TValue}"/></summary>
        public static IReadOnlyDictionary<TKey, TValue> ReadOnlyDictionary<TKey, TValue>() => EmptyDictionary<TKey, TValue>.Instance;

        #region ---- Empty Array ----
        private static class EmptyArray<TElement>
        {
            // this takes advantage of the fact that Enumerable.Empty() is currently implemented
            // using a cached empty array without depending on that fact
            public static readonly TElement[] Instance = (System.Linq.Enumerable.Empty<TElement>() as TElement[]) ?? new TElement[0];
        }
        #endregion

        #region ---- Empty Collection ----
        private class EmptyCollection<TElement> : IList<TElement>, IReadOnlyList<TElement>, ISet<TElement>, IEnumerator<TElement>, IList
        {
            public static readonly EmptyCollection<TElement> Instance = new EmptyCollection<TElement>();

            protected EmptyCollection() { }

            TElement IReadOnlyList<TElement>.this[int index]
            {
                get { throw ThrowCannotIndex(); }
            }

            object IList.this[int index]
            {
                get { throw ThrowCannotIndex(); }
                set { throw ThrowReadOnly(); }
            }

            TElement IList<TElement>.this[int index]
            {
                get { throw ThrowCannotIndex(); }
                set { throw ThrowReadOnly(); }
            }

            int IReadOnlyCollection<TElement>.Count => 0;

            int ICollection.Count => 0;

            int ICollection<TElement>.Count => 0;

            object IEnumerator.Current
            {
                // based on ((IEnumerator)new List<int>().GetEnumerator()).Current
                get { throw new InvalidOperationException("Enumeration has either not started or has already finished"); }
            }

            // based on new List<int>().GetEnumerator().Current
            TElement IEnumerator<TElement>.Current => default(TElement);

            bool IList.IsFixedSize => true;

            bool IList.IsReadOnly => true;

            bool ICollection<TElement>.IsReadOnly => true;

            bool ICollection.IsSynchronized => false;

            object ICollection.SyncRoot => this;

            int IList.Add(object value)
            {
                throw ThrowReadOnly();
            }

            bool ISet<TElement>.Add(TElement item)
            {
                throw ThrowReadOnly();
            }

            void ICollection<TElement>.Add(TElement item)
            {
                throw ThrowReadOnly();
            }

            void IList.Clear()
            {
                throw ThrowReadOnly();
            }

            void ICollection<TElement>.Clear()
            {
                throw ThrowReadOnly();
            }

            bool IList.Contains(object value) => false;

            bool ICollection<TElement>.Contains(TElement item) => false;

            void ICollection.CopyTo(Array array, int index)
            {
                if (array == null) { throw new ArgumentNullException(nameof(array)); }
                if (index < 0 || index > array.Length) { throw new ArgumentOutOfRangeException(nameof(index)); }
            }

            void ICollection<TElement>.CopyTo(TElement[] array, int arrayIndex)
            {
                if (array == null) { throw new ArgumentNullException(nameof(array)); }
                if (arrayIndex < 0 || arrayIndex > array.Length) { throw new ArgumentOutOfRangeException(nameof(arrayIndex)); }
            }

            void IDisposable.Dispose()
            {
            }

            void ISet<TElement>.ExceptWith(IEnumerable<TElement> other)
            {
                throw ThrowReadOnly();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this;
            }

            IEnumerator<TElement> IEnumerable<TElement>.GetEnumerator()
            {
                return this;
            }

            int IList.IndexOf(object value) => -1;

            int IList<TElement>.IndexOf(TElement item) => -1;

            void IList.Insert(int index, object value)
            {
                throw ThrowReadOnly();
            }

            void IList<TElement>.Insert(int index, TElement item)
            {
                throw ThrowReadOnly();
            }

            void ISet<TElement>.IntersectWith(IEnumerable<TElement> other)
            {
                throw ThrowReadOnly();
            }

            bool ISet<TElement>.IsProperSubsetOf(IEnumerable<TElement> other)
            {
                if (other == null) { throw new ArgumentNullException(nameof(other)); }
                return other.Any();
            }

            bool ISet<TElement>.IsProperSupersetOf(IEnumerable<TElement> other)
            {
                if (other == null) { throw new ArgumentNullException(nameof(other)); }
                return false;
            }

            bool ISet<TElement>.IsSubsetOf(IEnumerable<TElement> other)
            {
                if (other == null) { throw new ArgumentNullException(nameof(other)); }
                return true;
            }

            bool ISet<TElement>.IsSupersetOf(IEnumerable<TElement> other)
            {
                if (other == null) { throw new ArgumentNullException(nameof(other)); }
                return !other.Any();
            }

            bool IEnumerator.MoveNext() => false;

            bool ISet<TElement>.Overlaps(IEnumerable<TElement> other)
            {
                if (other == null) { throw new ArgumentNullException(nameof(other)); }
                return false;
            }

            void IList.Remove(object value)
            {
                throw ThrowReadOnly();
            }

            bool ICollection<TElement>.Remove(TElement item)
            {
                throw ThrowReadOnly();
            }

            void IList.RemoveAt(int index)
            {
                throw ThrowReadOnly();
            }

            void IList<TElement>.RemoveAt(int index)
            {
                throw ThrowReadOnly();
            }

            void IEnumerator.Reset()
            {
            }

            bool ISet<TElement>.SetEquals(IEnumerable<TElement> other)
            {
                if (other == null) { throw new ArgumentNullException(nameof(other)); }
                return !other.Any();
            }

            void ISet<TElement>.SymmetricExceptWith(IEnumerable<TElement> other)
            {
                throw ThrowReadOnly();
            }

            void ISet<TElement>.UnionWith(IEnumerable<TElement> other)
            {
                throw ThrowReadOnly();
            }

            private static Exception ThrowCannotIndex()
            {
                throw new ArgumentOutOfRangeException("Cannot index into an empty collection");
            }
        }
        #endregion

        #region ---- Empty Dictionary ----
        private sealed class EmptyDictionary<TKey, TValue> : EmptyCollection<KeyValuePair<TKey, TValue>>, IReadOnlyDictionary<TKey, TValue>, IDictionary<TKey, TValue>, IDictionary, IDictionaryEnumerator
        {
            public static new readonly EmptyDictionary<TKey, TValue> Instance = new EmptyDictionary<TKey, TValue>();

            private EmptyDictionary() { }

            object IDictionary.this[object key]
            {
                get { return null; }
                set { throw ThrowReadOnly(); }
            }

            TValue IDictionary<TKey, TValue>.this[TKey key]
            {
                get { throw new KeyNotFoundException(); }
                set { throw ThrowReadOnly(); }
            }

            TValue IReadOnlyDictionary<TKey, TValue>.this[TKey key]
            {
                get { throw new KeyNotFoundException(); }
            }
            
            bool IDictionary.IsFixedSize => true;

            bool IDictionary.IsReadOnly => true;
            
            ICollection IDictionary.Keys => ObjectCollection;

            ICollection<TKey> IDictionary<TKey, TValue>.Keys => Collection<TKey>();

            IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => Collection<TKey>();
            
            ICollection<TValue> IDictionary<TKey, TValue>.Values => Collection<TValue>();

            ICollection IDictionary.Values => ObjectCollection;

            IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => ReadOnlyCollection<TValue>();

            object IDictionaryEnumerator.Key
            {
                get { throw new InvalidOperationException("Enumeration has either not started or has already finished"); }
            }

            object IDictionaryEnumerator.Value
            {
                get { throw new InvalidOperationException("Enumeration has either not started or has already finished"); }
            }

            DictionaryEntry IDictionaryEnumerator.Entry
            {
                get { throw new InvalidOperationException("Enumeration has either not started or has already finished"); }
            }
            
            void IDictionary.Add(object key, object value)
            {
                throw ThrowReadOnly();
            }

            void IDictionary<TKey, TValue>.Add(TKey key, TValue value)
            {
                throw ThrowReadOnly();
            }

            void IDictionary.Clear()
            {
                throw ThrowReadOnly();
            }
            
            bool IDictionary.Contains(object key) => false;
            
            bool IDictionary<TKey, TValue>.ContainsKey(TKey key) => false;

            bool IReadOnlyDictionary<TKey, TValue>.ContainsKey(TKey key) => false;
            
            IDictionaryEnumerator IDictionary.GetEnumerator() => this;
            
            void IDictionary.Remove(object key)
            {
                throw ThrowReadOnly();
            }
            
            bool IDictionary<TKey, TValue>.Remove(TKey key)
            {
                throw ThrowReadOnly();
            }

            bool IDictionary<TKey, TValue>.TryGetValue(TKey key, out TValue value)
            {
                value = default(TValue);
                return false;
            }

            bool IReadOnlyDictionary<TKey, TValue>.TryGetValue(TKey key, out TValue value)
            {
                value = default(TValue);
                return false;
            }
        }
        #endregion

        private static Exception ThrowReadOnly([CallerMemberName] string memberName = null)
        {
            throw new NotSupportedException(memberName + ": the collection is read-only");
        }
    }
}
