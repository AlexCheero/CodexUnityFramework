using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace CodexFramework.Utils.Pools
{
    public interface IPoolableBehaviour
    {
        public PoolItem Item { get; }
    }
    
    public interface IResetOnGetPoolableBehaviour : IPoolableBehaviour
    {
        public void OnGet();
    }
    
    public interface IResetOnReturnPoolableBehaviour : IPoolableBehaviour
    {
        public void OnReturn();
    }
    
    public class PoolItem : MonoBehaviour
    {
        [SerializeField]
        private int _initialCount = 2;
        public int InitialCount => _initialCount;
        
        [SerializeField]
        private ObjectPool _pool;
        public ObjectPool Pool => _pool;
        
        [SerializeField]
        private int _idx;

        private Dictionary<Type, Component> _cachedComponents;
        private Dictionary<Type, Component[]> _cachedChildrenComponents;
        private IResetOnGetPoolableBehaviour[] _getPoolableBehaviours;
        private IResetOnReturnPoolableBehaviour[] _returnPoolableBehaviours;

        public int Idx => _idx;

        public void OnCreate()
        {
            _getPoolableBehaviours = GetComponents<IResetOnGetPoolableBehaviour>();
            _returnPoolableBehaviours = GetComponents<IResetOnReturnPoolableBehaviour>();
        }
        
        public void AddToPool(ObjectPool pool, int idx)
        {
            _pool = pool;
            _idx = idx;
        }

        public void OnGetFromPool()
        {
            for (var i = 0; i < _getPoolableBehaviours.Length; i++)
                _getPoolableBehaviours[i].OnGet();
        }

        public void ReturnToPool()
        {
            StopAllCoroutines();
            _pool.ReturnItem(this);
            
            for (var i = 0; i < _returnPoolableBehaviours.Length; i++)
                _returnPoolableBehaviours[i].OnReturn();
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