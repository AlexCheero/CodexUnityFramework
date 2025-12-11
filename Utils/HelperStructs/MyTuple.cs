using System;

namespace CodexFramework.Utils
{
    //TODO: remove and use C#'s tuple structs instead
    [Serializable]
    public struct MyTuple<T1, T2>
    {
        public T1 Item1;
        public T2 Item2;
    }
}