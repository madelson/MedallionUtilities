using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.KDTree
{
    internal static class Selection
    {
        public static int Select<T>(T[] array, int left, int right, int k, IComparer<T> comparer)
        {
            // down to one element: we found it!
            if (left == right) { return left; }

            // pick a pivot using the median-of-3
            var pivotIndex = MedianOfThree(array, left, right, comparer);

            // partition the segment, returning the new index of the pivot
            var partitionedPivotIndex = Partition(array, left, right, pivotIndex, comparer);

            // if the pivot ended up at index k, then we found the kth element. Otherwise, recurse
            return partitionedPivotIndex == k ? k
                : k < partitionedPivotIndex ? Select(array, left, partitionedPivotIndex - 1, k, comparer)
                : Select(array, partitionedPivotIndex + 1, right, k, comparer);
        }

        private static int MedianOfThree<T>(T[] array, int left, int right, IComparer<T> comparer)
        {
            var mid = left + ((right - left) / 2);
            if (comparer.Compare(array[right], array[left]) < 0)
            {
                Swap(ref array[left], ref array[right]);
            }
            if (comparer.Compare(array[mid], array[left]) < 0)
            {
                Swap(ref array[mid], ref array[left]);
            }
            if (comparer.Compare(array[right], array[mid]) < 0)
            {
                Swap(ref array[right], ref array[mid]);
            }
            return mid;
        }
        
        private static int Partition<T>(T[] array, int left, int right, int pivotIndex, IComparer<T> comparer)
        {
            var pivotValue = array[pivotIndex];

            var i = left - 1;
            var j = right + 1;
            while (true)
            {
                do { ++i; }
                while (comparer.Compare(array[i], pivotValue) <= 0);

                do { --j; }
                while (comparer.Compare(array[j], pivotValue) > 0);

                if (i >= j) { return j; }

                Swap(ref array[i], ref array[j]);
            }
        }

        internal static void Swap<T>(ref T a, ref T b)
        {
            var temp = a;
            a = b;
            b = temp;
        }
    }
}
