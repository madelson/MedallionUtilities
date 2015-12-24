using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.Collections
{
    public abstract class ReadOnlyDictionaryBase<TKey, TValue> : DictionaryBase<TKey, TValue>, IDictionary<TKey, TValue>
    {
        public TValue this[TKey key]
        {
            get { return this.GetValue(key); }
        }

        TValue IDictionary<TKey, TValue>.this[TKey key]
        {
            get { return this[key]; }
            set { throw ReadOnly(); }
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly
        {
            get { return this.IsReadOnly; }
        }

        ICollection<TKey> IDictionary<TKey, TValue>.Keys { get { return this.Keys; } }

        ICollection<TValue> IDictionary<TKey, TValue>.Values { get { return this.Values; } }

        void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item)
        {
            throw ReadOnly();
        }

        void IDictionary<TKey, TValue>.Add(TKey key, TValue value)
        {
            throw ReadOnly();
        }

        void ICollection<KeyValuePair<TKey, TValue>>.Clear()
        {
            throw ReadOnly();
        }

        public override bool Contains(KeyValuePair<TKey, TValue> item)
        {
            TValue value;
            return this.TryGetValue(item.Key, out value) && EqualityComparer<TValue>.Default.Equals(value, item.Value);
        } 

        void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            CopyToHelper(this, array, arrayIndex);
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
        {
            throw ReadOnly();
        }

        bool IDictionary<TKey, TValue>.Remove(TKey key)
        {
            throw ReadOnly();
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected sealed override void Add(TKey key, TValue value, bool throwIfPresent)
        {
            throw ReadOnly();
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected sealed override void InternalClear()
        {
            throw ReadOnly();
        }
    }
}
