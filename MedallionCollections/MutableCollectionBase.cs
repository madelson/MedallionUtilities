//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace Medallion.Collections
//{
//    public abstract class MutableCollectionBase<TElement> : CollectionBase<TElement>, ICollection<TElement>
//    {
//        bool ICollection<TElement>.IsReadOnly { get { return false; } }

//        public void Add(TElement item) { this.InternalAdd(item); }

//        protected abstract void InternalAdd(TElement item);

//        public abstract void Clear();

//        void ICollection<TElement>.CopyTo(TElement[] array, int arrayIndex)
//        {
//            CopyToHelper(this, array, arrayIndex);
//        }

//        public abstract bool Remove(TElement item);
//    }
//}
