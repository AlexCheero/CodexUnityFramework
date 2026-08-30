using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodexFramework.Utils
{
	//TODO: Unity Should have it's own implementation
    [Serializable]
    public class SerializableDict<K, V>
    {
        [SerializeField]
        private Pair<K, V>[] _pairs;
        private Dictionary<K, V> _dict;

        public Dictionary<K, V> Dict
        {
            get
            {
                _dict ??= ConvertToDict(_pairs);
                return _dict;
            }
        }

        private Dictionary<K, V> ConvertToDict(Pair<K, V>[] pairs)
        {
            var set = new Dictionary<K, V>(pairs.Length);
            for (int i = 0; i < pairs.Length; i++)
                set.Add(pairs[i].Item1, pairs[i].Item2);
            pairs = null;

            return set;
        }
        
        public V this[K key]
        {
            get
            {
                _dict ??= ConvertToDict(_pairs);
#if DEBUG
                if (!_dict.ContainsKey(key))
                    throw new IndexOutOfRangeException("Set have no such entry!");
#endif
                return _dict[key];
            }
        }

        public bool Have(K key)
        {
            _dict ??= ConvertToDict(_pairs);
            return _dict.ContainsKey(key);
        }
    }
}