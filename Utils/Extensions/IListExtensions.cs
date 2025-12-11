using System.Collections.Generic;

namespace CodexFramework.Utils
{
    static class IListExtensions
    {
        public static void Shuffle<T>(this IList<T> list)
        {
            var n = list.Count;
            while (n > 1)
            {
                n--;
                var k = UnityEngine.Random.Range(0, n + 1);
                (list[n], list[k]) = (list[k], list[n]);
            }
        }

        public static T GetRandomItem<T>(this IList<T> list)
        {
            return list[UnityEngine.Random.Range(0, list.Count)];
        }

        public delegate bool MergeSortComparator<T>(T a, T b);
        public static void InPlaceMergeSort<T>(this IList<T> list, MergeSortComparator<T> comparator, int low = 0) =>
            list.InPlaceMergeSort(comparator, low, list.Count - 1);
        public static void InPlaceMergeSort<T>(this IList<T> list, MergeSortComparator<T> comparator, int low, int high)
        {
            if (low < high)
            {
                int middle = low + (high - low) / 2;

                list.InPlaceMergeSort(comparator, low, middle);
                list.InPlaceMergeSort(comparator, middle + 1, high);

                list.Merge(comparator, low, middle, high);
            }
        }

        public static void Merge<T>(this IList<T> list, MergeSortComparator<T> comparator, int low, int middle, int high)
        {
            int i = low;
            int j = middle + 1;

            while (i <= middle && j <= high)
            {
                if (comparator(list[j], list[i]))
                {
                    i++;
                }
                else
                {
                    var value = list[j];
                    int index = j;

                    // Shift all the elements between element i and j to the right by one.
                    while (index != i)
                    {
                        list[index] = list[index - 1];
                        index--;
                    }
                    list[i] = value;

                    // Adjust the pointers
                    i++;
                    middle++;
                    j++;
                }
            }
        }
    }
}