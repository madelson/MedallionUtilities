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
        public static IEnumerable Enumerable { get { return Empty<object>.Enumerable; } }
        public static ICollection Collection { get { return List; } }
        public static IList List { get { return (IList)Empty<object>.List; } }
        public static IDictionary Dictionary { get { return (IDictionary)Empty<string, object>.Dictionary; } }
    }
}
