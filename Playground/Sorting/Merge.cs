using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.Sorting
{
    public static class Merge
    {
        //private static void Merge<T>(T[] array, int start1, int start2, int end2, IComparer<T> comparer)
        //{
        //    int workingAreaStart;

        //    while (start1 < start2 && comparer.Compare(array[start1], array[start2]) <= 0)
        //    {
        //        ++start1;
        //    }

        //    if (start1 < start2)
        //    {
        //        do
        //        {
        //            Swap(array, start1++, start2);
        //        }
        //        while (start1 < start2 && comparer.Compare(array[start2], array[start2 + 1]) <= 0);

        //        workingAreaStart = start2;
        //        ++start2;


        //    }
        //}

        private static void Swap<T>(T[] array, int a, int b)
        {
            var temp = array[a];
            array[a] = array[b];
            array[b] = temp;
        }
    }
}
