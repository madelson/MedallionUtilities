using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.KDTree
{
    public static class KDPointComparer<TPoint>
    {
        public static IKDPointComparer<TPoint> Default { get; } = null;

        // these go in non-generic class
        //public static IKDPointComparer<TElement[]> ForArray<TElement>(int length) { throw new Exception(); }

        //public static IKDPointComparer<IReadOnlyList<TElement>> ForReadOnlyList<TElement>(int length) { throw new Exception(); }

        //public static IKDPointComparer<IList<TElement>> ForList<TElement>(int length) { throw new Exception(); }

        //private static IKDPointComparer<TPoint> CreateDefaultComparer()
        //{

        //}

        private sealed class KDPointComparableComparer : IKDPointComparer<TPoint>
        {
            public int Dimensions
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            public int Compare(TPoint a, TPoint b, int dimension)
            {
                throw new NotImplementedException();
            }
        }

        //private sealed class TupleComparer<T1> : IKDPointComparer<T1>
        //{
        //    public int Dimensions => 1;
        //    public int Compare(T1 a, T1 b, int dimension)
        //    {
        //        if (dimension != 1) { throw new ArgumentOutOfRangeException(nameof(dimension)); }

        //        return Comparer<T1>.Default.Compare(a, b);
        //    }
        //}
    }
}
