using System.Collections.Generic;
using UnityEngine;

namespace CodexFramework.Utils.Pools
{
    public class PoolManager : Singleton<PoolManager>
    {
        private readonly Dictionary<PoolItem, ObjectPool> _poolsList = new();

        protected override void Init()
        {
            base.Init();
            _poolsList.Clear();
        }

        public ObjectPool GetByPrototype(IPoolableBehaviour prototype) => GetByPrototype(prototype.Item);
        public ObjectPool GetByPrototype(PoolItem prototype) => GetByPrototype(prototype, prototype.InitialCount);
        
        public ObjectPool GetByPrototype(IPoolableBehaviour prototype, int createIfNotFoundWithSize) => GetByPrototype(prototype.Item, createIfNotFoundWithSize);
        public ObjectPool GetByPrototype(PoolItem prototype, int createIfNotFoundWithSize)
        {
            if (!_poolsList.ContainsKey(prototype))
                _poolsList[prototype] = CreatePool(prototype, createIfNotFoundWithSize);
            return _poolsList[prototype];
        }

        private ObjectPool CreatePool(PoolItem prototype, int createIfNotFoundWithSize)
        {
            ObjectPool pool = null;
            if (createIfNotFoundWithSize > 0)
            {
                pool = new GameObject(prototype.name + "Pool").AddComponent<ObjectPool>();
                pool.Init(createIfNotFoundWithSize, prototype);
                _poolsList.Add(prototype, pool);
            }

            return pool;
        }
    }
}