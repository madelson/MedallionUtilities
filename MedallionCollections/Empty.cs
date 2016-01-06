using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.Collections
{
    public static class Empty
    {
        public static IEnumerable Enumerable => Empty<object>.Enumerable;
        public static ICollection Collection => List;
        public static IList List => (IList)Empty<object>.List;
        public static IDictionary Dictionary => (IDictionary)Empty<string, object>.Dictionary;
    }
}
