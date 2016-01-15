//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace Medallion.Collections
//{
//    public abstract class ReadOnlyCollectionBase<TElement> : CollectionBase<TElement>, ICollection<TElement>
//    {
//        int ICollection<TElement>.Count { get { return this.Count; } }

//        bool ICollection<TElement>.IsReadOnly { get { return true; } }

//        void ICollection<TElement>.Add(TElement item)
//        {
//            throw ReadOnly();
//        }

//        void ICollection<TElement>.Clear()
//        {
//            throw ReadOnly();
//        }

//        void ICollection<TElement>.CopyTo(TElement[] array, int arrayIndex)
//        {
//            CopyToHelper(this, array, arrayIndex);
//        }

//        bool ICollection<TElement>.Remove(TElement item)
//        {
//            throw ReadOnly();
//        }
//    }
//}
