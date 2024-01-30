using System.Collections.Generic;
using UnityEngine;

namespace CodexFramework.Utils.Collections
{
    public class BucketedList<T>
    {
        public const int BucketSize = Constants.MaxInstancedObjectsPerCall;

        public int Count { get; private set; }

        public List<List<T>> Lists { get; private set; }

        public BucketedList()
        {
            Lists = new();
            Count = 0;
        }

        public void Clear()
        {
            foreach (var list in Lists)
                list.Clear();
            Count = 0;
        }

        public void Add(T element)
        {
            var lastListIdx = (int)((float)Count / BucketSize);

#if DEBUG
            if (lastListIdx > Lists.Count)
                Debug.LogError("BucketedList.Count error");

            while (lastListIdx >= Lists.Count)
#else
        if (lastListIdx >= Lists.Count)
#endif
                Lists.Add(new List<T>());
            Lists[lastListIdx].Add(element);
            Count++;
        }
    }
}