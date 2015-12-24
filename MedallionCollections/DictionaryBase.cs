using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.Collections
{
    public abstract class DictionaryBase<TKey, TValue> : CollectionBase<KeyValuePair<TKey, TValue>>, IReadOnlyDictionary<TKey, TValue>, IDictionary
    {
        TValue IReadOnlyDictionary<TKey, TValue>.this[TKey key]
        {
            get { return this.GetValue(key); }
        }

        protected TValue GetValue(TKey key)
        {
            TValue value;
            if (!this.TryGetValue(key, out value))
            {
                throw new KeyNotFoundException();
            }
            return value;
        }

        private static byte canKeyTypeBeNullCache;

        private static bool CanKeyTypeBeNull()
        {
            const byte CanBeNull = 1, CannotBeNull = 2;
            var cachedValue = canKeyTypeBeNullCache;
            if (cachedValue != 0)
            {
                return cachedValue == CanBeNull;
            }

            var result = !typeof(TKey).IsValueType || Nullable.GetUnderlyingType(typeof(TKey)) != null;
            canKeyTypeBeNullCache = result ? CanBeNull : CannotBeNull;
            return result;
        }

        protected abstract void Add(TKey key, TValue value, bool throwIfPresent);

        object IDictionary.this[object key]
        {
            get
            {
                TValue value;
                if (((key == null && CanKeyTypeBeNull()) || (key is TKey))
                    && this.TryGetValue((TKey)key, out value))
                {
                    return value;
                }

                return null;
            }
            set
            {
                this.Add(key, value, throwIfPresent: false);
            }
        }

        private void Add(object key, object value, bool throwIfPresent)
        {
            // note: this try/catch approach is based on Dictionary<,>

            TKey typedKey;
            try
            {
                typedKey = (TKey)key;
            }
            catch (InvalidCastException ex)
            {
                throw new ArgumentException($"{nameof(key)} must be of type {typeof(TKey)}", ex);
            }

            TValue typedValue;
            try
            {
                typedValue = (TValue)value;
            }
            catch (InvalidCastException ex)
            {
                throw new ArgumentException($"{nameof(value)} must be of type {typeof(TValue)}", ex);
            }

            this.Add(typedKey, typedValue, throwIfPresent);
        }

        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys { get { return this.Keys; } }

        private sealed class KeyCollection : ReadOnlyCollectionBase<TKey>
        {
            private readonly DictionaryBase<TKey, TValue> dictionary;

            public KeyCollection(DictionaryBase<TKey, TValue> dictionary)
            {
                this.dictionary = dictionary;
            }

            public override int Count { get { return this.dictionary.Count; } }

            public override bool Contains(TKey item)
            {
                return this.dictionary.ContainsKey(item);
            }

            protected override IEnumerator<TKey> InternalGetEnumerator()
            {
                return this.dictionary.Select(kvp => kvp.Key).GetEnumerator();
            }
        }

        private KeyCollection keys;

        public ReadOnlyCollectionBase<TKey> Keys
        {
            get { return this.keys ?? (this.keys = new KeyCollection(this)); }
        }

        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values { get { return this.Values; } }

        private sealed class ValueCollection : ReadOnlyCollectionBase<TValue>
        {
            private DictionaryBase<TKey, TValue> dictionary;

            public ValueCollection(DictionaryBase<TKey, TValue> dictionary)
            {
                this.dictionary = dictionary;
            }

            public override int Count { get { return this.dictionary.Count; } }

            public override bool Contains(TValue item)
            {
                return Enumerable.Contains(this, item);
            }

            protected override IEnumerator<TValue> InternalGetEnumerator()
            {
                return this.dictionary.Select(kvp => kvp.Value).GetEnumerator();
            }
        }

        private ValueCollection values;

        protected ReadOnlyCollectionBase<TValue> Values
        {
            get { return this.values ?? (this.values = new ValueCollection(this)); }
        }

        bool IDictionary.IsFixedSize { get { return this.IsReadOnly; } }

        bool IDictionary.IsReadOnly { get { return this.IsReadOnly; } }

        protected bool IsReadOnly { get; }

        ICollection IDictionary.Keys { get { return this.Values; } }

        ICollection IDictionary.Values { get { return this.Values; } }

        public virtual bool ContainsKey(TKey key)
        {
            TValue ignored;
            return this.TryGetValue(key, out ignored);
        }

        public abstract bool TryGetValue(TKey key, out TValue value);

        void IDictionary.Add(object key, object value)
        {
            this.Add(key, value, throwIfPresent: true);
        }

        void IDictionary.Clear()
        {
            this.InternalClear();
        }

        protected abstract void InternalClear();

        bool IDictionary.Contains(object key)
        {
            return ((key == null && CanKeyTypeBeNull()) || key is TKey)
                && this.ContainsKey((TKey)key);
        }

        IDictionaryEnumerator IDictionary.GetEnumerator()
        {
            return new DictionaryEnumerator(this.GetEnumerator());
        }

        private sealed class DictionaryEnumerator : IDictionaryEnumerator
        {
            private readonly IEnumerator<KeyValuePair<TKey, TValue>> enumerator;
            private bool isEnumerating;

            public DictionaryEnumerator(IEnumerator<KeyValuePair<TKey, TValue>> enumerator)
            {
                this.enumerator = enumerator;
            }

            object IEnumerator.Current
            {
                get
                {
                    this.CheckEnumerating();
                    return this.enumerator.Current;
                }
            }

            DictionaryEntry IDictionaryEnumerator.Entry
            {
                get
                {
                    this.CheckEnumerating();
                    return new DictionaryEntry(this.enumerator.Current.Key, this.enumerator.Current.Value);
                }
            }

            object IDictionaryEnumerator.Key
            {
                get
                {
                    this.CheckEnumerating();
                    return this.enumerator.Current.Key;
                }
            }

            object IDictionaryEnumerator.Value
            {
                get
                {
                    this.CheckEnumerating();
                    return this.enumerator.Current.Value;
                }
            }

            bool IEnumerator.MoveNext()
            {
                return (this.isEnumerating = this.enumerator.MoveNext());
            }

            void IEnumerator.Reset()
            {
                this.enumerator.Reset();
                this.isEnumerating = false;
            }

            private void CheckEnumerating()
            {
                if (!this.isEnumerating)
                {
                    throw new InvalidOperationException("Enumeration has either not started or has already finished");
                }
            }
        }

        void IDictionary.Remove(object key)
        {
            if ((key == null && CanKeyTypeBeNull()) || key is TKey)
            {
                this.InternalRemove((TKey)key);
            }
        }

        protected abstract bool InternalRemove(TKey key);
    }
}
