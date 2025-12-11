using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace CodexFramework.Utils
{
    [Serializable]
    public struct Trigger<T>
    {
        [SerializeField]
        private T _t;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Check()
        {
            var result = _t;
            _t = default;
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(T t) => _t = t;
    }
}