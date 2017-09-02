using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.IndexedCollection
{
    #region ---- Index Interfaces ----
    public interface IIndex<TKey>
    {
        bool Equals(TKey @this, TKey that);
    }

    public interface IUniqueIndex<TKey> : IIndex<TKey>
    {
        IIndex<TKey> BaseIndex { get; }
    }

    public interface ISortedIndex<TKey> : IIndex<TKey>
    {
        IComparer<TKey> Comparer { get; }
    }

    public interface IUniqueSortedIndex<TKey> : IUniqueIndex<TKey>, ISortedIndex<TKey>
    {
        new ISortedIndex<TKey> BaseIndex { get; }
    }
    #endregion

    #region ---- ReadOnly Interfaces ----
    public interface IReadOnlySortedList<TKey, TValue> : IReadOnlyList<KeyValuePair<TKey, TValue>>
    {
        IComparer<TKey> Comparer { get; }

        int IndexOfKey(TKey key);
        int IndexOfKey(TKey key, int startIndex);
        int LastIndexOfKey(TKey key);
        int LastIndexOfKey(TKey key, int startIndex);
    }

    public interface IReadOnlySortedDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>, IReadOnlySortedList<TKey, TValue>
    {
    }

    public interface IReadOnlyIndexedCollection<T> : IReadOnlyList<T>
    {
        bool HasIndex<TKey>(IIndex<TKey> index);

        ILookup<TKey, T> GetIndexLookup<TKey>(IIndex<TKey> index);
        IReadOnlySortedList<TKey, T> GetIndexList<TKey>(ISortedIndex<TKey> index);
        IReadOnlyDictionary<TKey, T> GetIndexDictionary<TKey>(IUniqueIndex<TKey> index);
        IReadOnlySortedDictionary<TKey, T> GetIndexDictionary<TKey>(IUniqueSortedIndex<TKey> index);

        bool TryGetValue<TKey>(TKey key, IUniqueIndex<TKey> index, out T value);
        IEnumerable<T> GetValues<TKey>(TKey key, IIndex<TKey> index);
        IOrderedEnumerable<T> GetSortedValues<TKey>(TKey key, ISortedIndex<TKey> index);

        // todo can multiple collections really share an index?
        IEnumerable<(T, TOther)> Join<TOther, TKey>(IReadOnlyIndexedCollection<TOther> collection, IIndex<TKey> index);
        IEnumerable<IGrouping<T, TOther>> GroupJoin<TOther, TKey>(IReadOnlyIndexedCollection<TOther> collection, IIndex<TKey> index);
    }

    public interface IReadOnlyUniqueIndexedCollection<T> : IReadOnlyIndexedCollection<T>
    {
        IUniqueIndex<T> PrimaryIndex { get; } 
    }

    public interface IReadOnlySortedIndexedCollection<T> : IReadOnlyIndexedCollection<T>
    {
        ISortedIndex<T> PrimaryIndex { get; }
    }
    #endregion

    #region ---- Mutable Interfaces ----
    public interface IIndexedCollection<T> : IReadOnlyIndexedCollection<T>, ICollection<T>
    {
        IIndex<TKey> AddIndex<TKey>(Func<T, TKey> keySelector, IEqualityComparer<TKey> comparer = null);
        ISortedIndex<TKey> AddSortedIndex<TKey>(Func<T, TKey> keySelector, IComparer<TKey> comparer = null);
        IUniqueIndex<TKey> AddUniqueIndex<TKey>(Func<T, TKey> keySelector, IEqualityComparer<TKey> comparer = null);
        IUniqueSortedIndex<TKey> AddUniqueSortedIndex<TKey>(Func<T, TKey> keySelector, IComparer<TKey> comparer = null);

        // todo do these APIs really make any sense?
        void AddIndex<TKey>(IIndex<TKey> index);
        void AddUniqueIndex<TKey>(IIndex<TKey> index);
        void AddUniqueSortedIndex<TKey>(ISortedIndex<TKey> index);
    }
    #endregion
}
