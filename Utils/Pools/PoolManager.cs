using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodexFramework.Utils.Pools
{
    public class PoolManager : Singleton<PoolManager>
    {
        private readonly List<ObjectPool> _poolsList = new List<ObjectPool>();

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
            ObjectPool pool = null;
            foreach (var p in _poolsList)
            {
                if (p.PrototypeGO == prototype.gameObject)
                {
                    pool = p;
                    break;
                }
            }

            if (pool != null)
                return pool;

            _poolsList.Clear();
            _poolsList.AddRange(FindObjectsOfType<ObjectPool>());

            foreach (var p in _poolsList)
            {
                if (p.PrototypeGO == prototype.gameObject)
                {
                    pool = p;
                    break;
                }
            }
            if (pool == null)
                pool = CreatePool(prototype, createIfNotFoundWithSize);
            return pool;
        }

        private ObjectPool CreatePool(PoolItem prototype, int createIfNotFoundWithSize)
        {
            ObjectPool pool = null;
            if (createIfNotFoundWithSize > 0)
            {
                pool = new GameObject(prototype.name + "Pool").AddComponent<ObjectPool>();
                pool.Init(createIfNotFoundWithSize, prototype);
                _poolsList.Add(pool);
            }

            return pool;
        }
    }
}