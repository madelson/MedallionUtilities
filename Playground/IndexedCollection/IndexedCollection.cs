using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.IndexedCollection
{
    public class IndexedCollection<T> : IIndexedCollection<T>
    {
        private readonly List<T> _list = new List<T>();
        private readonly List<IInternalIndex> _indices = new List<IInternalIndex>();

        public T this[int index] => this._list[index];

        public int Count => this._list.Count;

        public bool IsReadOnly => false;

        public void Add(T item)
        {
            var i = 0;
            var needsRevert = false;
            try
            {
                while (i < this._indices.Count)
                {
                    if (!this._indices[i].TryAdd(item))
                    {
                        needsRevert = true;
                        break;
                    }
                    ++i;
                }
            }
            catch
            {
                needsRevert = true;
                throw;
            }
            finally
            {
                if (needsRevert)
                {
                    for (var j = 0; j < i; ++j)
                    {
                        this._indices[j].Remove(item);
                    }
                }
            }

            this._list.Add(item);
        }

        public IIndex<TKey> AddIndex<TKey>(Func<T, TKey> keySelector, IEqualityComparer<TKey> comparer = null)
        {
            if (keySelector == null) { throw new ArgumentNullException(nameof(keySelector)); }

            var newIndex = new HashIndex<TKey>(keySelector, comparer);
            this.InternalAddIndex(newIndex);
            return newIndex;
        }

        private void InternalAddIndex(IInternalIndex index)
        {
            foreach (var item in this._list)
            {
                if (!index.TryAdd(item))
                {
                    throw new InvalidOperationException("todo");
                }
            }
        }

        public void AddIndex<TKey>(IIndex<TKey> index)
        {
            throw new NotImplementedException();
        }

        public ISortedIndex<TKey> AddSortedIndex<TKey>(Func<T, TKey> keySelector, IComparer<TKey> comparer = null)
        {
            throw new NotImplementedException();
        }

        public IUniqueIndex<TKey> AddUniqueIndex<TKey>(Func<T, TKey> keySelector, IEqualityComparer<TKey> comparer = null)
        {
            throw new NotImplementedException();
        }

        public void AddUniqueIndex<TKey>(IIndex<TKey> index)
        {
            throw new NotImplementedException();
        }

        public IUniqueSortedIndex<TKey> AddUniqueSortedIndex<TKey>(Func<T, TKey> keySelector, IComparer<TKey> comparer = null)
        {
            throw new NotImplementedException();
        }

        public void AddUniqueSortedIndex<TKey>(ISortedIndex<TKey> index)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            this._list.Clear();
            foreach (var index in this._indices)
            {
                index.Clear();
            }
        }

        public bool Contains(T item) => this._list.Contains(item);

        public void CopyTo(T[] array, int arrayIndex) => this._list.CopyTo(array, arrayIndex);

        public IEnumerator<T> GetEnumerator() => this._list.GetEnumerator();

        public IReadOnlyDictionary<TKey, T> GetIndexDictionary<TKey>(IUniqueIndex<TKey> index)
        {
            throw new NotImplementedException();
        }

        public IReadOnlySortedDictionary<TKey, T> GetIndexDictionary<TKey>(IUniqueSortedIndex<TKey> index)
        {
            throw new NotImplementedException();
        }

        public IReadOnlySortedList<TKey, T> GetIndexList<TKey>(ISortedIndex<TKey> index)
        {
            throw new NotImplementedException();
        }

        public ILookup<TKey, T> GetIndexLookup<TKey>(IIndex<TKey> index)
        {
            throw new NotImplementedException();
        }

        public IOrderedEnumerable<T> GetSortedValues<TKey>(TKey key, ISortedIndex<TKey> index)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<T> GetValues<TKey>(TKey key, IIndex<TKey> index)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<IGrouping<T, TOther>> GroupJoin<TOther, TKey>(IReadOnlyIndexedCollection<TOther> collection, IIndex<TKey> index)
        {
            throw new NotImplementedException();
        }

        public bool HasIndex<TKey>(IIndex<TKey> index)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<(T, TOther)> Join<TOther, TKey>(IReadOnlyIndexedCollection<TOther> collection, IIndex<TKey> index)
        {
            throw new NotImplementedException();
        }

        public bool Remove(T item)
        {
            throw new NotImplementedException();
        }

        public bool TryGetValue<TKey>(TKey key, IUniqueIndex<TKey> index, out T value)
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }

        private interface IInternalIndex
        {
            bool TryAdd(T item);
            void Remove(T item);
            void Clear();
        }

        private interface IInternalIndex<TKey> : IInternalIndex, IIndex<TKey>, ILookup<TKey, T>
        {
        }

        private class HashIndex<TKey> : Dictionary<ValueTuple<TKey>, (T item, List<T> items)>, IInternalIndex<TKey>
        {
            private readonly Func<T, TKey> _keySelector;
            
            public HashIndex(Func<T, TKey> keySelector, IEqualityComparer<TKey> comparer)
                : base(KeyComparer.For(comparer))
            {
                this._keySelector = keySelector;
            }

            bool IIndex<TKey>.Equals(TKey @this, TKey that)
            {
                return this.Comparer.Equals(new ValueTuple<TKey>(@this), new ValueTuple<TKey>(that));
            }

            void IInternalIndex.Remove(T item)
            {
                var key = new ValueTuple<TKey>(this._keySelector(item));
                if (this.TryGetValue(key, out var entry))
                {
                    if (entry.items != null) { entry.items.Remove(item); }
                    else { this.Remove(key); }
                }
            }

            bool IInternalIndex.TryAdd(T item)
            {
                var key = new ValueTuple<TKey>(this._keySelector(item));
                if (this.TryGetValue(key, out var entry))
                {
                    if (entry.items != null) { entry.items.Add(item); }
                    else { this[key] = (item: default(T), items: new List<T> { entry.item, item }); }
                }
                else
                {
                    this.Add(key, (item: item, items: null));
                }
                return true;
            }

            bool ILookup<TKey, T>.Contains(TKey key)
            {
                return this.ContainsKey(new ValueTuple<TKey>(key));
            }

            public IEnumerable<T> this[TKey key]
            {
                get
                {
                    return this.TryGetValue(new ValueTuple<TKey>(key), out var entry)
                        ? (entry.items ?? (IEnumerable<T>)new[] { entry.item })
                        : Enumerable.Empty<T>();
                }
            }

            IEnumerator<IGrouping<TKey, T>> IEnumerable<IGrouping<TKey, T>>.GetEnumerator()
            {
                throw new NotImplementedException();
            }

            //private sealed class Grouping : IGrouping<TKey, T>
            //{
            //    public Grouping(TKey key, IEnumerable<T> values)
            //    {
            //    }
            //}

            private sealed class KeyComparer : IEqualityComparer<ValueTuple<TKey>>
            {
                private static KeyComparer _default;

                private readonly IEqualityComparer<TKey> _comparer;

                private KeyComparer(IEqualityComparer<TKey> comparer)
                {
                    this._comparer = comparer;
                }

                public static KeyComparer For(IEqualityComparer<TKey> comparer)
                {
                    if (comparer != null && !comparer.Equals(EqualityComparer<TKey>.Default))
                    {
                        return new KeyComparer(comparer);
                    }

                    return _default ?? (_default = new KeyComparer(EqualityComparer<TKey>.Default));
                }

                bool IEqualityComparer<ValueTuple<TKey>>.Equals(ValueTuple<TKey> x, ValueTuple<TKey> y)
                {
                    return this._comparer.Equals(x.Item1, y.Item1);
                }

                int IEqualityComparer<ValueTuple<TKey>>.GetHashCode(ValueTuple<TKey> obj)
                {
                    return this._comparer.GetHashCode(obj.Item1);
                }
            }
        }
    }
}
