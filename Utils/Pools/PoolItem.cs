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
        
        public int InitialCount => _initialCount;
        public int MaxCount => _maxCount;
        public int GrowPerFrame => _growPerFrame;
        
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
        private bool _isReturning;
        private int _leaseVersion;
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
            SetPoolIndex(pool, idx);
            _isReturning = false;
            _isInPool = true;
        }

        internal void SetPoolIndex(ObjectPool pool, int idx)
        {
            _pool = pool;
            _idx = idx;
        }

        internal int MarkCheckedOut()
        {
            _isReturning = false;
            _isInPool = false;
            _leaseVersion = unchecked(_leaseVersion + 1);
            return _leaseVersion;
        }

        internal int LeaseVersion => _leaseVersion;
        internal bool IsReturning => _isReturning;

        internal void InvokeOnGetCallbacks()
        {
            for (var i = 0; i < _getPoolableBehaviours.Length; i++)
                _getPoolableBehaviours[i].OnGet();
        }

        public void OnGetFromPool()
        {
            MarkCheckedOut();
            InvokeOnGetCallbacks();
        }

        public void ReturnToPool() => _pool.ReturnItem(this);

        internal void MarkReturning()
        {
            _isReturning = true;
            _isInPool = true;
        }

        internal void MarkReturned()
        {
            _isReturning = false;
            _isInPool = true;
        }

        internal void InvokeOnReturnCallbacks()
        {
            for (var i = 0; i < _returnPoolableBehaviours.Length; i++)
                _returnPoolableBehaviours[i].OnReturn();
        }

        //TODO: bad design- OnReturn is called from ObjectPool.ReturnItem
        public void OnReturn()
        {
            MarkReturning();
            try
            {
                InvokeOnReturnCallbacks();
            }
            finally
            {
                MarkReturned();
            }
        }

        private void OnDestroy()
        {
            if (_pool)
                _pool.NotifyItemDestroyed();
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