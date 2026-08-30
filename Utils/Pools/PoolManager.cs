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
        public ObjectPool GetByPrototype(PoolItem prototype) => GetByPrototype(prototype, prototype.InitialCount, prototype.MaxCount);
        
        public ObjectPool GetByPrototype(IPoolableBehaviour prototype, int initialCount, int maxCount) => GetByPrototype(prototype.Item, initialCount, maxCount);
        public ObjectPool GetByPrototype(PoolItem prototype, int initialCount, int maxCount)
        {
            if (!_poolsDict.ContainsKey(prototype))
                _poolsDict[prototype] = CreatePool(prototype, initialCount, maxCount);
            return _poolsDict[prototype];
        }

        private ObjectPool CreatePool(PoolItem prototype, int initialCount, int maxCount)
        {
            ObjectPool pool = null;
            if (initialCount > 0)
            {
                pool = new GameObject(prototype.name + "Pool").AddComponent<ObjectPool>();
                pool.Init(prototype, initialCount, maxCount);
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