using System;

namespace CodexFramework.Utils
{
    [Serializable]
    public struct Pair<T1, T2>
    {
        public T1 Item1;
        public T2 Item2;
    }
}