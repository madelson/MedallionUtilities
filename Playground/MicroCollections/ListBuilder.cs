using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.MicroCollections
{
    public static class ListBuilder
    {
        private static class Empty<T>
        {
            public static T[] Array = Enumerable.Empty<T>() as T[] ?? new T[0];
            public static IReadOnlyList<T> ReadOnlyList => Array;
        }

        public static void Add<T>(ref List<T> list, T item) => (list ?? (list = new List<T>())).Add(item);

        public static void AddRange<T>(ref List<T> list, IEnumerable<T> items)
        {
            if (list == null)
            {
                list = new List<T>(items);
            }
            else
            {
                list.AddRange(items);
            }
        }

        public static IReadOnlyList<T> ToReadOnlyList<T>(List<T> list) => list ?? Empty<T>.ReadOnlyList;
    }
}
