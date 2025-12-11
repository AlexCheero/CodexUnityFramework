using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace CodexFramework.Utils
{
    [Serializable]
    public struct WithPrev<T>
    {
        [SerializeField]
        private T _val;
        private T _prev;

        public T Val
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _val;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                _prev = _val;
                _val = value;
            }
        }
        
        public T  Prev
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _prev;
        }
    }
}