using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace CodexFramework.Utils.Pools
{
    public class PoolItem : MonoBehaviour
    {
        [SerializeField]
        private ObjectPool _pool;
        [SerializeField]
        private int _idx;

        private Dictionary<Type, Component> _cachedComponents;
        private Dictionary<Type, Component[]> _cachedChildrenComponents;

        public int Idx => _idx;

        public bool IsFirstUse { get; private set; } = true;

        public void AddToPool(ObjectPool pool, int idx)
        {
            _pool = pool;
            _idx = idx;
        }

        public virtual void ReturnToPool()
        {
            StopAllCoroutines();
            IsFirstUse = false;
            _pool.ReturnItem(this);
        }

        private bool _isDelayedReturning;
        public void DelayedReturnToPool(float delay)
        {
            if (!_isDelayedReturning)
                StartCoroutine(DelayedReturnToPoolRoutine(delay));
        }

        private IEnumerator DelayedReturnToPoolRoutine(float delay)
        {
            _isDelayedReturning = true;
            yield return new WaitForSeconds(delay);
            ReturnToPool();
            _isDelayedReturning = false;
        }

        public T[] GetAllComponentsInChildrenAndCache<T>(bool includeInactive = false) where T : Component
        {
            _cachedChildrenComponents ??= new Dictionary<Type, Component[]>();
            var key = typeof(T);
            if (!_cachedChildrenComponents.ContainsKey(key))
                _cachedChildrenComponents[key] = GetComponentsInChildren<T>(includeInactive);
            return _cachedChildrenComponents[key] as T[];
        }

        public T GetComponentAndCache<T>() where T : Component
        {
            _cachedComponents ??= new Dictionary<Type, Component>();
            var key = typeof(T);
            if (!_cachedComponents.ContainsKey(key))
                _cachedComponents[key] = GetComponent<T>();
            return _cachedComponents[key] as T;
        }

        public T AddComponentAndCache<T>() where T : Component
        {
            _cachedComponents ??= new Dictionary<Type, Component>();
            var key = typeof(T);
            _cachedComponents[key] = gameObject.GetOrAddComponent<T>();
            return _cachedComponents[key] as T;
        }
    }
}