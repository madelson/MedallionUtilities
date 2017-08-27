using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.ByRef
{
    public interface IByRefComparer<T>
    {
        int Compare(ref T a, ref T b);
    }

    public interface IByRefEqualityComparer<T>
    {
        bool Equals(ref T a, ref T b);
        int GetHashCode(ref T obj);
    }
}
