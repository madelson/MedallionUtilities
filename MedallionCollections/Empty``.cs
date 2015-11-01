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
        public static IReadOnlyDictionary<TKey, TValue> Dictionary { get { return EmptyDictionary.Instance; } }
        public static IDictionary<TKey, TValue> FrozenDictionary { get { return EmptyDictionary.Instance; } }

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

            int ICollection.Count { get { return 0; } }

            int ICollection<KeyValuePair<TKey, TValue>>.Count { get { return 0; } }

            int IReadOnlyCollection<KeyValuePair<TKey, TValue>>.Count { get { return 0; } }

            bool IDictionary.IsFixedSize { get { return true; } }

            bool IDictionary.IsReadOnly { get { return true; } }

            bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly { get { return true; } }

            bool ICollection.IsSynchronized { get { return false; } }

            ICollection IDictionary.Keys { get { return Empty.Collection; } }

            ICollection<TKey> IDictionary<TKey, TValue>.Keys { get { return Empty<TKey>.FrozenCollection; } }

            IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys { get { return Empty<TKey>.Collection; } }

            object ICollection.SyncRoot { get { return this; } }

            ICollection<TValue> IDictionary<TKey, TValue>.Values { get { return Empty<TValue>.FrozenCollection; } }

            ICollection IDictionary.Values { get { return Empty.Collection; } }

            IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values { get { return Empty<TValue>.Collection; } }

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

            bool IDictionary.Contains(object key)
            {
                return false;
            }

            bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item)
            {
                return false;
            }

            bool IDictionary<TKey, TValue>.ContainsKey(TKey key)
            {
                return false;
            }

            bool IReadOnlyDictionary<TKey, TValue>.ContainsKey(TKey key)
            {
                return false;
            }

            void ICollection.CopyTo(Array array, int index)
            {
                Empty.Collection.CopyTo(array, index);
            }

            void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
            {
                Empty<KeyValuePair<TKey, TValue>>.FrozenCollection.CopyTo(array, arrayIndex);
            }

            IDictionaryEnumerator IDictionary.GetEnumerator()
            {
                return this;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this;
            }

            IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
            {
                return Empty<KeyValuePair<TKey, TValue>>.Collection.GetEnumerator();
            }

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

            bool IEnumerator.MoveNext() { return false; }

            void IEnumerator.Reset() { }

            private static Exception ThrowReadOnly([CallerMemberName] string memberName = null)
            {
                throw new InvalidOperationException(memberName + ": the collection is read-only");
            }
        }
    }
}
