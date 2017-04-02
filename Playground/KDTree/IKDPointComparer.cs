using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.KDTree
{
    public interface IKDPointComparer<in TPoint>
    {
        int Dimensions { get; }
        int Compare(TPoint a, TPoint b, int dimension);
    }

    internal interface INeedsInitializationKDPointComparer<in TPoint> : IKDPointComparer<TPoint>
    {
        void InitializeFrom(TPoint point);
    }
}
