using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Medallion.Collections
{
    // TODO
    // CB, ROCB, DB, RODB

    public abstract class CollectionBase<T> : ICollection<T>, IReadOnlyCollection<T>, ICollection
    {
        public abstract int Count { get; }
        
        protected abstract bool IsReadOnly { get; }

        bool ICollection<T>.IsReadOnly { get { return this.IsReadOnly; } }

        bool ICollection.IsSynchronized { get { return false; } }

        private object syncRoot;

        object ICollection.SyncRoot
        {
            get
            {
                var syncRoot = this.syncRoot;
                return syncRoot == null
                    ? Interlocked.CompareExchange(ref this.syncRoot, new object(), comparand: null)
                    : syncRoot;
            }
        }

        public abstract void Add(T item);

        public abstract void Clear();

        bool ICollection<T>.Contains(T item)
        {
            return Enumerable.Contains(this, item);
        }

        void ICollection.CopyTo(Array array, int index)
        {
            throw new NotImplementedException();
        }

        void ICollection<T>.CopyTo(T[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.AsEnumerable().GetEnumerator();
        }

        public abstract IEnumerator<T> GetEnumerator();

        bool ICollection<T>.Remove(T item)
        {
            throw new NotImplementedException();
        }
    }
}
