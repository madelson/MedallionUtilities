//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Linq;
//using System.Runtime.CompilerServices;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;

//namespace Medallion.Collections
//{
//    // TODO
//    // CB, ROCB, DB, RODB

//    public abstract class CollectionBase<TElement> : IReadOnlyCollection<TElement>, ICollection
//    {
//        public abstract int Count { get; }

//        bool ICollection.IsSynchronized { get { return false; } }

//        private object _syncRoot;
//        object ICollection.SyncRoot
//        {
//            get { return LazyInitializer.EnsureInitialized(ref this._syncRoot, () => new object()); }
//        }

//        void ICollection.CopyTo(Array array, int index)
//        {
//            Throw.IfNull(array, nameof(array));
//            Throw.If(array.Rank != 1, nameof(array) + " must be one-dimensional");
//            Throw.If(index < 0, nameof(index) + "must be non-negative");
//            Throw.If(index + this.Count > array.Length, nameof(array) + " is not long enough");

//            var i = index;
//            foreach (var element in this)
//            {
//                array.SetValue(element, i++);
//            }
//        }

//        IEnumerator IEnumerable.GetEnumerator() { return this.InternalGetEnumerator(); }

//        public IEnumerator<TElement> GetEnumerator() { return this.InternalGetEnumerator(); }

//        protected abstract IEnumerator<TElement> InternalGetEnumerator();

//        public abstract bool Contains(TElement item);

//        internal static NotSupportedException ReadOnly([CallerMemberName] string member = null)
//        {
//            return new NotSupportedException($"{member} is not supported: the collection is read-only");
//        }

//        internal static void CopyToHelper(IReadOnlyCollection<TElement> collection, TElement[] array, int arrayIndex)
//        {
//            Throw.IfNull(array, nameof(array));
//            Throw.If(arrayIndex + collection.Count > array.Length, nameof(array) + " is not long enough");

//            var i = arrayIndex;
//            foreach (var element in collection)
//            {
//                array[i++] = element;
//            }
//        }
//    }
//}
