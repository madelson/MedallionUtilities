using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.Collections
{
    public static class Empty<TKey, TValue>
    {
        public static IReadOnlyDictionary<TKey, TValue> Dictionary => EmptyDictionary.Instance;
        public static IDictionary<TKey, TValue> FrozenDictionary => EmptyDictionary.Instance;

        private sealed class EmptyDictionary : IReadOnlyDictionary<TKey, TValue>, IDictionary<TKey, TValue>, IDictionary, IDictionaryEnumerator
        {
            public static readonly EmptyDictionary Instance = new EmptyDictionary();

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

            int ICollection.Count => 0;

            int ICollection<KeyValuePair<TKey, TValue>>.Count => 0;

            int IReadOnlyCollection<KeyValuePair<TKey, TValue>>.Count => 0;

            bool IDictionary.IsFixedSize => true;

            bool IDictionary.IsReadOnly => true;

            bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => true;

            bool ICollection.IsSynchronized => false;

            ICollection IDictionary.Keys => Empty.Collection;

            ICollection<TKey> IDictionary<TKey, TValue>.Keys => Empty<TKey>.FrozenCollection;

            IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => Empty<TKey>.Collection;

            object ICollection.SyncRoot => this;

            ICollection<TValue> IDictionary<TKey, TValue>.Values => Empty<TValue>.FrozenCollection;

            ICollection IDictionary.Values => Empty.Collection;

            IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => Empty<TValue>.Collection;

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

            object IEnumerator.Current
            {
                get { throw new InvalidOperationException("Enumeration has either not started or has already finished"); }
            }

            void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item)
            {
                throw ThrowReadOnly();
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

            void ICollection<KeyValuePair<TKey, TValue>>.Clear()
            {
                throw ThrowReadOnly();
            }

            bool IDictionary.Contains(object key) => false;

            bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item) => false;

            bool IDictionary<TKey, TValue>.ContainsKey(TKey key) => false;

            bool IReadOnlyDictionary<TKey, TValue>.ContainsKey(TKey key) => false;

            void ICollection.CopyTo(Array array, int index) => Empty.Collection.CopyTo(array, index);

            void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) 
                => Empty<KeyValuePair<TKey, TValue>>.FrozenCollection.CopyTo(array, arrayIndex);

            IDictionaryEnumerator IDictionary.GetEnumerator() => this;

            IEnumerator IEnumerable.GetEnumerator() => this;

            IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
                => Empty<KeyValuePair<TKey, TValue>>.Collection.GetEnumerator();

            void IDictionary.Remove(object key)
            {
                throw ThrowReadOnly();
            }

            bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
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

            bool IEnumerator.MoveNext() => false;

            void IEnumerator.Reset() { }

            private static Exception ThrowReadOnly([CallerMemberName] string memberName = null)
            {
                throw new InvalidOperationException(memberName + ": the collection is read-only");
            }
        }
    }
}
