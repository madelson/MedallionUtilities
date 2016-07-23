using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.Collections
{
    [DebuggerTypeProxy(typeof(PriorityQueue<>.DebugView))]
    [DebuggerDisplay("Count = {Count}")]
    public sealed class PriorityQueue<T> : ICollection<T>, IReadOnlyCollection<T>
    {
        private static readonly T[] EmptyArray = Enumerable.Empty<T>() as T[] ?? new T[0];

        private readonly IComparer<T> comparer;
        private T[] heap;
        private int version;

        #region ---- Constructors ----
        public PriorityQueue(IComparer<T> comparer = null)
        {
            this.comparer = comparer ?? Comparer<T>.Default;
            this.heap = EmptyArray;
        }

        public PriorityQueue(int initialCapacity, IComparer<T> comparer = null)
            : this(comparer)
        {
            if (initialCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), initialCapacity, "must be positive");
            }

            if (initialCapacity > 0)
            {
                this.heap = new T[initialCapacity];
            }
        }

        public PriorityQueue(IEnumerable<T> items, IComparer<T> comparer = null)
            : this(comparer)
        {
            if (items == null) { throw new ArgumentNullException(nameof(items)); }

            ICollection<T> itemsCollection;
            IReadOnlyCollection<T> readOnlyItemsCollection; 
            if ((itemsCollection = items as ICollection<T>) != null)
            {
                if (itemsCollection.Count > 0)
                {
                    this.heap = new T[itemsCollection.Count];
                    itemsCollection.CopyTo(this.heap, arrayIndex: 0);
                    this.Count = itemsCollection.Count;
                }
            }
            else if ((readOnlyItemsCollection = items as IReadOnlyCollection<T>) != null)
            {
                if (readOnlyItemsCollection.Count > 0)
                {
                    this.heap = new T[readOnlyItemsCollection.Count];
                    foreach (var item in readOnlyItemsCollection)
                    {
                        this.heap[this.Count++] = item;
                    }
                }
            }
            else
            {
                foreach (var item in items)
                {
                    if (this.Count == this.heap.Length)
                    {
                        this.Expand();
                    }
                    this.heap[this.Count++] = item;
                }
            }

            this.Heapify();
        }
        #endregion

        #region ---- Priority Queue API ----
        public void Enqueue(T item)
        {
            if (this.heap.Length == this.Count)
            {
                this.Expand();
            }

            var lastIndex = this.Count;
            ++this.Count;
            this.Swim(lastIndex, item);
            ++this.version;
        }

        public T Dequeue()
        {
            if (this.Count == 0) { throw new InvalidOperationException("The priority queue is empty"); }

            var result = this.heap[0];
            --this.Count;
            var last = this.heap[this.Count];
            // don't hold a reference
            this.heap[this.Count] = default(T);
            this.Sink(0, last);

            ++this.version;
            return result;
        }

        public T Peek()
        {
            if (this.Count == 0) { throw new InvalidOperationException("The priority queue is empty"); }

            return this.heap[0];
        }

        public void EnqueueRange(IEnumerable<T> items)
        {
            if (items == null) { throw new ArgumentNullException(nameof(items)); }


        }
        #endregion
        
        #region ---- Heap Maintenance Methods ----
        private void Expand()
        {
            var currentCapacity = (uint)this.heap.Length;

            if (currentCapacity == int.MaxValue)
            {
                throw new InvalidOperationException("Queue capacity cannot be further expanded");
            }

            // small heaps grow faster than large ones
            // based on the java implementation http://grepcode.com/file/repository.grepcode.com/java/root/jdk/openjdk/6-b14/java/util/PriorityQueue.java#243
            const int InitialCapacity = 16;

            var newCapacity = currentCapacity < InitialCapacity ? InitialCapacity
                : currentCapacity < 64 ? 2 * (currentCapacity + 1)
                : 3 * (currentCapacity / 2);
            Array.Resize(ref this.heap, (int)Math.Min(newCapacity, int.MaxValue));
        }

        private void Sink(int index, T item)
        {
            // from http://grepcode.com/file/repository.grepcode.com/java/root/jdk/openjdk/6-b14/java/util/PriorityQueue.java#653

            var half = this.Count >> 1;
            var i = index;
            while (i < half)
            {
                // pick the lesser of the left and right children
                var childIndex = (i << 1) + 1;
                var childItem = this.heap[childIndex];
                var rightChildIndex = childIndex + 1;
                if (rightChildIndex < this.Count && this.comparer.Compare(childItem, this.heap[rightChildIndex]) > 0)
                {
                    childItem = this.heap[childIndex = rightChildIndex];
                }

                // if item is <= either child, stop
                if (this.comparer.Compare(item, childItem) <= 0)
                {
                    break;
                }

                // move smaller child up and move our pointer down the heap
                this.heap[i] = childItem;
                i = childIndex;
            }

            // finally, put the initial item in the final position
            this.heap[i] = item;
        }

        private void Swim(int index, T item)
        {
            // from http://grepcode.com/file/repository.grepcode.com/java/root/jdk/openjdk/6-b14/java/util/PriorityQueue.java#607

            var i = index;
            while (i > 0)
            {
                // find the parent index/item
                var parentIndex = (i - 1) >> 1;
                var parentItem = this.heap[parentIndex];

                // if the parent is <= item, we're done
                if (this.comparer.Compare(item, parentItem) >= 0)
                {
                    break;
                }

                // shift the parent down and traverse our pointer up
                this.heap[i] = parentItem;
                i = parentIndex;
            }

            // finally, leave item at the final position
            this.heap[i] = item;
        }

        private void Heapify()
        {
            // from http://grepcode.com/file/repository.grepcode.com/java/root/jdk/openjdk/6-b14/java/util/PriorityQueue.java#673

            for (var i = (this.Count >> 1) - 1; i >= 0; --i)
            {
                this.Sink(i, this.heap[i]);
            }
        }

        private void AppendItems(IEnumerable<T> items)
        {
            ICollection<T> itemsCollection;
            IReadOnlyCollection<T> readOnlyItemsCollection;
            if ((itemsCollection = items as ICollection<T>) != null)
            {
                if (itemsCollection.Count > 0)
                {
                    this.heap = new T[checked(this.Count + itemsCollection.Count)];
                    itemsCollection.CopyTo(this.heap, arrayIndex: this.Count);
                    this.Count = itemsCollection.Count;
                }
            }
            else if ((readOnlyItemsCollection = items as IReadOnlyCollection<T>) != null)
            {
                if (readOnlyItemsCollection.Count > 0)
                {
                    this.heap = new T[checked(this.Count + readOnlyItemsCollection.Count)];
                    foreach (var item in readOnlyItemsCollection)
                    {
                        this.heap[this.Count++] = item;
                    }
                }
            }
            else
            {
                foreach (var item in items)
                {
                    if (this.Count == this.heap.Length)
                    {
                        this.Expand();
                    }
                    this.heap[checked(this.Count++)] = item;
                }
            }
        }
        #endregion

        #region ---- Interface Members ----
        public int Count { get; private set; }

        bool ICollection<T>.IsReadOnly => false;

        [EditorBrowsable(EditorBrowsableState.Never)]
        public void Add(T item) => this.Enqueue(item);

        public void Clear()
        {
            const int MaxRetainedCapacity = 1024;

            if (this.heap.Length > MaxRetainedCapacity)
            {
                this.heap = EmptyArray;
            }
            else
            {
                Array.Clear(this.heap, index: 0, length: this.Count);
            }

            this.Count = 0;
            ++this.version;
        }

        public bool Contains(T item) => this.Find(item).HasValue;
        
        void ICollection<T>.CopyTo(T[] array, int arrayIndex)
        {
            // performs any bounds checks
            Array.Copy(sourceArray: this.heap, sourceIndex: 0, destinationArray: array, destinationIndex: arrayIndex, length: this.Count);
        }
        
        public IEnumerator<T> GetEnumerator()
        {
            var version = this.version;

            for (var i = 0; i < this.Count; ++i)
            {
                if (this.version != version) { throw new InvalidOperationException("Collection was modified; enumeration operation may not execute."); }
                yield return this.heap[i];
            }
        }

        public bool Remove(T item)
        {
            var itemIndex = this.Find(item);

            if (itemIndex.HasValue)
            {
                var indexToRemove = itemIndex.Value;
                --this.Count;
                var lastItem = this.heap[this.Count];
                this.heap[this.Count] = default(T);
                if (indexToRemove != this.Count)
                {
                    this.Sink(indexToRemove, lastItem);
                }

                ++this.version;
                return true;
            }

            return false;
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
        #endregion

        private int? Find(T item) => this.Count > 0 ? this.FindHelper(item, startIndex: 0) : null;

        private int? FindHelper(T item, int startIndex)
        {
            var cmp = this.comparer.Compare(item, this.heap[startIndex]);
            if (cmp == 0)
            {
                // found it!
                return startIndex;
            }
            if (cmp < 0)
            {
                // if item < root of sub-heap, it can't appear in the sub-heap
                return null;
            }

            var leftChildIndex = (startIndex << 1) + 1;
            return leftChildIndex >= this.Count || leftChildIndex < 0 
                // return null if left child index oveflowed or was past the end of the heap
                ? null 
                : (
                    // else search the left subtree
                    this.FindHelper(item, leftChildIndex)
                    // if that returns null, search the right subtree unless left child index was the end of the heap
                    // in which case right child index would be past the end
                    ?? (leftChildIndex == this.Count - 1 ? null : this.FindHelper(item, leftChildIndex + 1))
                 );             
        }

        private sealed class DebugView
        {
            private readonly PriorityQueue<T> queue;

            public DebugView(PriorityQueue<T> queue)
            {
                if (queue == null) { throw new ArgumentNullException(nameof(queue)); }

                this.queue = queue;
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public T[] Items => this.queue.ToArray();
        }
    }
}
