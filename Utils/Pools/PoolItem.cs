using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
        [SerializeField]
        private int _maxCount = -1;
        [SerializeField]
        private int _growPerFrame = 1;
        [SerializeField]
        [Tooltip("When returning, place this item at the end of the free list so it is reused last.")]
        private bool _reuseReturnedLast;

        public int InitialCount => _initialCount;
        public int MaxCount => _maxCount;
        public int GrowPerFrame => _growPerFrame;
        public bool ReuseReturnedLast => _reuseReturnedLast;
        
        [SerializeField]
        private ObjectPool _pool;
        public ObjectPool Pool => _pool;
        
        [SerializeField]
        private int _idx;

        private Dictionary<Type, Component> _cachedComponents;
        private Dictionary<Type, Component[]> _cachedChildrenComponents;
        private IResetOnGetPoolableBehaviour[] _getPoolableBehaviours;
        private IResetOnReturnPoolableBehaviour[] _returnPoolableBehaviours;

        public int Idx
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _idx;
        }

        private bool _isInPool;
        public bool IsInPool
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _isInPool;
        }

        public void OnCreate()
        {
            _getPoolableBehaviours = GetComponents<IResetOnGetPoolableBehaviour>();
            _returnPoolableBehaviours = GetComponents<IResetOnReturnPoolableBehaviour>();
        }
        
        public void AddToPool(ObjectPool pool, int idx)
        {
            _pool = pool;
            _idx = idx;
            _isInPool = true;
        }

        public void OnGetFromPool()
        {
            _isInPool = false;
            for (var i = 0; i < _getPoolableBehaviours.Length; i++)
                _getPoolableBehaviours[i].OnGet();
        }

        public void ReturnToPool() => _pool.ReturnItem(this);

        //TODO: bad design- OnReturn is called from ObjectPool.ReturnItem
        public void OnReturn()
        {
            for (var i = 0; i < _returnPoolableBehaviours.Length; i++)
                _returnPoolableBehaviours[i].OnReturn();
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