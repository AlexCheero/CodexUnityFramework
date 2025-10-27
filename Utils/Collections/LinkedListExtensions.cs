using System;
using System.Collections.Generic;

namespace CodexFramework.Utils.Collections
{
    public static class LinkedListExtensions
    {
        public static void PushFirst<T>(this LinkedList<T> list, T item) => list.AddFirst(item);

        public static void PushLast<T>(this LinkedList<T> list, T item) => list.AddLast(item);

        public static T PopFirst<T>(this LinkedList<T> list)
        {
            if (list.Count == 0)
                throw new InvalidOperationException("Deque is empty.");

            var item = list.First.Value;
            list.RemoveFirst();
            return item;
        }

        public static T PopLast<T>(this LinkedList<T> list)
        {
            if (list.Count == 0)
                throw new InvalidOperationException("Deque is empty.");

            var item = list.Last.Value;
            list.RemoveLast();
            
            return item;
        }

        public static T PeekFirst<T>(this LinkedList<T> list)
        {
            if (list.Count == 0)
                throw new InvalidOperationException("Deque is empty.");

            return list.First.Value;
        }

        public static T PeekLast<T>(this LinkedList<T> list)
        {
            if (list.Count == 0)
                throw new InvalidOperationException("Deque is empty.");

            return list.Last.Value;
        }
    }
}