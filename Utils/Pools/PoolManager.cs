using System.Collections.Generic;
using UnityEngine;

namespace CodexFramework.Utils.Pools
{
    public class PoolManager : Singleton<PoolManager>
    {
        private readonly Dictionary<PoolItem, ObjectPool> _poolsDict = new();

        protected override void Init()
        {
            base.Init();
            _poolsDict.Clear();
        }

        public ObjectPool GetByPrototype(IPoolableBehaviour prototype) => GetByPrototype(prototype.Item);
        public ObjectPool GetByPrototype(PoolItem prototype) => GetByPrototype(prototype, prototype.InitialCount);
        
        public ObjectPool GetByPrototype(IPoolableBehaviour prototype, int createIfNotFoundWithSize) => GetByPrototype(prototype.Item, createIfNotFoundWithSize);
        public ObjectPool GetByPrototype(PoolItem prototype, int createIfNotFoundWithSize)
        {
            if (!_poolsDict.ContainsKey(prototype))
                _poolsDict[prototype] = CreatePool(prototype, createIfNotFoundWithSize);
            return _poolsDict[prototype];
        }

        private ObjectPool CreatePool(PoolItem prototype, int createIfNotFoundWithSize)
        {
            ObjectPool pool = null;
            if (createIfNotFoundWithSize > 0)
            {
                pool = new GameObject(prototype.name + "Pool").AddComponent<ObjectPool>();
                pool.Init(createIfNotFoundWithSize, prototype);
                _poolsDict.Add(prototype, pool);
            }

            return pool;
        }
        
#if UNITY_EDITOR
        [SerializeField]
        private bool _logAllocated;

        void OnDestroy()
        {
            if (!_logAllocated)
                return;

            foreach (var pool in _poolsDict.Values)
                Debug.Log($"{pool.Prototype.name}'s pool allocated: {pool.Allocated}");
        }
#endif
    }
}