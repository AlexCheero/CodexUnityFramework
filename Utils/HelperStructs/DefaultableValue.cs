using System;
using UnityEngine;

namespace CodexFramework.Utils
{
    [Serializable]
    public struct DefaultableValue<T> where T : struct
    {
        private T? _initialValue;
        [SerializeField]
        private T _value;

        public T InitialValue { get { _initialValue ??= _value; return _initialValue.Value; } }
        public T Value
        {
            get => _value;
            set { _initialValue ??= _value; _value = value; }
        }

        public DefaultableValue(T value) => _initialValue = _value = value;

        public void Reset() => _value = InitialValue;
    }
}