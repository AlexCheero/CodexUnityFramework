using System;

namespace CodexFramework.Utils
{
    [Serializable]
    public struct WeightedValue<T>
    {
        public T Value;
        public float Weight;
    }
}