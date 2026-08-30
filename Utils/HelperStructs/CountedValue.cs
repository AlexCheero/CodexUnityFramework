using System;

namespace CodexFramework.Utils
{
    [Serializable]
    public struct CountedValue<T>
    {
        public T Value;
        public int Count;
    }
}