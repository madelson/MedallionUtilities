using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Playground.MicroCache
{
    public sealed class MicroCache<TKey, TValue>
    {
        // alg: starts with just HT
        // any read touches an element (+1)
        // when passes threshold, uses quickselect to identify N/2 "bad" elements by lowest count
        // all good elements get "aged" by 1/2
        // after that, keeps replacing bad elements on write until all gone: then we have to QS again
        // O(N) work every N/2 writes: O(1) maint cost!

        // TODO: what to do for non-reference-type keys?
        // TODO: should we do anything special for even smaller cases?

        private readonly int maxCount;
        private readonly Hashtable hashTable;

        private int remainingColdItems;
        private KeyValuePair<TKey, ValueHolder>[] quickSelectArray;

        public MicroCache(int maxCount, IEqualityComparer<TKey> comparer = null)
        {
            if (maxCount < 1) { throw new ArgumentOutOfRangeException(nameof(maxCount), maxCount, "must be positive"); }

            this.maxCount = maxCount;
            this.hashTable = new Hashtable(
                comparer == null ? null : (comparer as IEqualityComparer) ?? new ObjectEqualityComparer(comparer)
            );
        }

        private object Lock => this.hashTable;

        public bool TryGetValue(TKey key, out TValue value)
        {
            if (key == null) { throw new ArgumentNullException(nameof(key)); }

            var holder = (ValueHolder)this.hashTable[key];
            if (holder != null)
            {
                value = holder.Value;
                holder.Touch();
                return true;
            }

            value = default(TValue);
            return false;
        }

        public bool TryAdd(TKey key, TValue value)
        {
            if (key == null) { throw new ArgumentNullException(nameof(key)); }

            var holder = (ValueHolder)this.hashTable[key];
            if (holder != null) { return false; }
            
            lock (this.Lock)
            {
                // double-checked locking
                holder = (ValueHolder)this.hashTable[key];
                if (holder != null) { return false; }
                
                this.AddNoLock(key, new ValueHolder(value));
                return true;
            }
        }

        public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
        {
            if (key == null) { throw new ArgumentNullException(nameof(key)); }
            if (valueFactory == null) { throw new ArgumentNullException(nameof(valueFactory)); }

            TValue existing;
            if (this.TryGetValue(key, out existing)) { return existing; }

            var created = valueFactory(key);
            lock (this.Lock)
            {
                // double-checked locking
                var holder = (ValueHolder)this.hashTable[key];
                if (holder != null)
                {
                    holder.Touch();
                    return holder.Value;
                }

                this.AddNoLock(key, new ValueHolder(created));
                return created;
            }
        }

        private void AddNoLock(TKey key, ValueHolder value)
        {
            if (this.hashTable.Count == this.maxCount)
            {
                // first, make sure we have cold items to remove
                if (this.remainingColdItems == 0)
                {
                    this.IdentifyColdItemsNoLock();
                }

                // make space by kicking out a cold item
                var indexToRemove = this.remainingColdItems + 1;
                var keyToRemove = this.quickSelectArray[indexToRemove].Key;
                this.hashTable.Remove(key);
                this.quickSelectArray[indexToRemove] = new KeyValuePair<TKey, ValueHolder>(key, value);
                --this.remainingColdItems;
                this.hashTable.Add(key, value);
            }
        }

        private void IdentifyColdItemsNoLock()
        {
            if (this.quickSelectArray == null)
            {
                this.quickSelectArray = new KeyValuePair<TKey, ValueHolder>[this.maxCount];
                var i = 0;
                foreach (DictionaryEntry entry in this.hashTable)
                {
                    this.quickSelectArray[i++] = new KeyValuePair<TKey, ValueHolder>((TKey)entry.Key, (ValueHolder)entry.Value);
                }
            }

            // items [0..k] will become cold. We want to make ~half the items cold in one go, rounded up so that a max count of 1 will
            // still leave us with one cold item
            var k = (this.maxCount / 2) + (this.maxCount & 1);
            Medallion.KDTree.Selection.Select(this.quickSelectArray, left: 0, right: this.maxCount - 1, k: k, comparer: UseCountComparer.Instance);
            this.remainingColdItems = k;
        } 

        private int GetUseCount(int index) => ((ValueHolder)this.hashTable[this.quickSelectArray[index]]).UseCount;

        private sealed class ValueHolder
        {
            private int useCount = 1;

            public ValueHolder(TValue value)
            {
                this.Value = value;
            }

            public TValue Value { get; }
            public int UseCount => this.useCount;

            public void Touch()
            {
                // note: this is not thread-safe so this will miss some
                // touches. However, adding additional overhead (locks or retried atomic ops)
                // doesn't seem worth it since we are inherently approximating the value
                // of having a particular item in cache
                var currentCount = this.useCount;
                if (currentCount < int.MaxValue)
                {
                    this.useCount = currentCount + 1;
                }
            }

            public void Age()
            {
                // see comment in Touch() for why lack of thread-safety here is ok
                this.useCount /= 2;
            }
        }

        private sealed class UseCountComparer : IComparer<KeyValuePair<TKey, ValueHolder>>
        {
            public static readonly UseCountComparer Instance = new UseCountComparer();

            int IComparer<KeyValuePair<TKey, ValueHolder>>.Compare(KeyValuePair<TKey, ValueHolder> x, KeyValuePair<TKey, ValueHolder> y)
            {
                return x.Value.UseCount.CompareTo(y.Value.UseCount);
            }
        }

        // todo think about the case where TKey is a value type!
        private sealed class ObjectEqualityComparer : IEqualityComparer
        {
            private readonly IEqualityComparer<TKey> comparer;

            public ObjectEqualityComparer(IEqualityComparer<TKey> comparer)
            {
                this.comparer = comparer;
            }

            public bool Equals(object x, object y) => this.comparer.Equals((TKey)x, (TKey)y);

            public int GetHashCode(object obj) => this.comparer.GetHashCode((TKey)obj);
        }
    }
}
