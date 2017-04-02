using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.Sorting
{
    public static class Sorting
    {
        // todo likely also want range APIs as well

        // could also offer keyed sorting with a list of key values, we can either wrapperize this as a KVP list and sort
        // that OR if not required to be in place we could make an int array and sort that based on keys, then copy back
        // like the orderby enumerable does

        // sample sort could be a good choice for starting parallel sorts. Seems like it doesn't need to impact stability

        // block sort offers stable, in place sorting

        // idea for stable partition
        // (1) pick a pivot
        // (2) count # < pivot to determine sub-list sizes
        // (3) start with element 0 and go forward until we find one element in first that should switch to second
        // (4) move the element to second and advance the start of second
        // (4) then, until we run out of elements
        //      (a) decide whether the replaced element goes in first or second
        //      (b) if moving to first, replace the last element in first / if going in second replace the first element in second
        //      (d) increase/decrease the start/end pointer
        //      (c) repeat with replaced element
        //  WRONG: can't keep placing at the back of second because later values in second end up earlier, can't keep placing at
        //      the front of first since later values in first could end up ahead of earlier values in second
        //  FIX? 4 segments original firsts (builds from front), new firsts (builds from back, reverses), new seconds (builds from front and back), original seconds
        //      - does every part need to build from front and back... doesn't work since there are so many sources coming in
        
           

        public static void Sort<T>(IList<T> list, IComparer<T> comparer = null, SortOptions options = default(SortOptions))
        {
            if (list == null) { throw new ArgumentNullException(nameof(list)); }

            Sort<T, IList<T>, IComparer<T>>(list, comparer ?? Comparer<T>.Default, options);
        }

        public static void Sort<T>(T[] array, IComparer<T> comparer = null, SortOptions options = default(SortOptions))
        {
            if (array == null) { throw new ArgumentNullException(nameof(array)); }

            Sort<T, ArrayWrapper<T>, IComparer<T>>(new ArrayWrapper<T>(array), comparer ?? Comparer<T>.Default, options);
        }

        public static void Sort<T, TComparer>(IList<T> list, TComparer comparer, SortOptions options = default(SortOptions))
            where TComparer : struct, IComparer<T>
        {
            if (list == null) { throw new ArgumentNullException(nameof(list)); }

            Sort<T, IList<T>, TComparer>(list, comparer, options);
        }

        public static void Sort<T, TComparer>(T[] array, TComparer comparer, SortOptions options = default(SortOptions))
            where TComparer : struct, IComparer<T>
        {
            if (array == null) { throw new ArgumentNullException(nameof(array)); }

            Sort<T, ArrayWrapper<T>, TComparer>(new ArrayWrapper<T>(array), comparer, options);
        }

        private static void Sort<T, TCollection, TComparer>(TCollection list, TComparer comparer, SortOptions options)
            where TCollection : IList<T>
            where TComparer : IComparer<T>
        {

        }

        #region ---- ArrayWrapper ----
        /// <summary>
        /// Struct type which wraps an array. When running under optimization, the JIT is able to generate
        /// code which runs very close to the same as if we'd used array directory. This thus allows us
        /// to write a single generic implementation over <see cref="IList{T}"/> without sacrificing
        /// performance on arrays
        /// </summary>
        private struct ArrayWrapper<T> : IList<T>
        {
            private readonly T[] array;

            public ArrayWrapper(T[] array)
            {
                this.array = array;
            }

            public T this[int index]
            {
                get { return this.array[index]; }
                set { this.array[index] = value; }
            }

            public int Count => this.array.Length;

            public bool IsReadOnly
            {
                get
                {
                    return ((IList<T>)array).IsReadOnly;
                }
            }

            public void Add(T item)
            {
                ((IList<T>)array).Add(item);
            }

            public void Clear()
            {
                ((IList<T>)array).Clear();
            }

            public bool Contains(T item)
            {
                return ((IList<T>)array).Contains(item);
            }

            public void CopyTo(T[] array, int arrayIndex)
            {
                ((IList<T>)this.array).CopyTo(array, arrayIndex);
            }

            public IEnumerator<T> GetEnumerator()
            {
                return ((IList<T>)array).GetEnumerator();
            }

            public int IndexOf(T item)
            {
                return ((IList<T>)array).IndexOf(item);
            }

            public void Insert(int index, T item)
            {
                ((IList<T>)array).Insert(index, item);
            }

            public bool Remove(T item)
            {
                return ((IList<T>)array).Remove(item);
            }

            public void RemoveAt(int index)
            {
                ((IList<T>)array).RemoveAt(index);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return ((IList<T>)array).GetEnumerator();
            }
        }
        #endregion
    }

    public struct SortOptions
    {
        public bool Stable { get; set; }
        public bool InPlace { get; set; }
        public int? MaxDegreeOfParallelism { get; set; }
    }
}
