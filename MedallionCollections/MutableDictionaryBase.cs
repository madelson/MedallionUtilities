using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.Collections
{
    public abstract class MutableDictionaryBase<TKey, TValue> : DictionaryBase<TKey, TValue>, IDictionary<TKey, TValue>
    {
        public TValue this[TKey key]
        {
            get { return this.GetValue(key); }
            set { this.Add(key, value, throwIfPresent: false); }
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly
        {
            get { return this.IsReadOnly; }
        }

        ICollection<TKey> IDictionary<TKey, TValue>.Keys
        {
            get { return this.Keys; }
        }

        ICollection<TValue> IDictionary<TKey, TValue>.Values
        {
            get { return this.Values; }
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            this.Add(item.Key, item.Value);
        }

        public void Add(TKey key, TValue value)
        {
            this.Add(key, value, throwIfPresent: true);
        }

        void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            CopyToHelper(this, array, arrayIndex);
        }

        public virtual bool Remove(KeyValuePair<TKey, TValue> item)
        {
            TValue value;
            return this.TryGetValue(item.Key, out value)
                && EqualityComparer<TValue>.Default.Equals(value, item.Value)
                && this.InternalRemove(item.Key);
        }
        
        public bool Remove(TKey key)
        {
            return this.InternalRemove(key);
        }

        public void Clear() { this.InternalClear(); }
    }
}
