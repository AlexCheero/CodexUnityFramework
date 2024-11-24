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

        public ObjectPool GetByPrototype(
            PoolItem prototype,
            int createIfNotFoundWithSize = 16,
            Action<PoolItem> onGet = null,
            Action<PoolItem> onReturn = null)
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
            {
                if (!pool.IsOnGetBinded)
                    pool.OnGetItem += onGet;
                if (!pool.IsOnReturnBinded)
                    pool.OnReturnItem += onReturn;

                return pool;
            }

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
                pool = CreatePool(prototype, createIfNotFoundWithSize, onGet, onReturn);
            return pool;
        }

        private ObjectPool CreatePool(
            PoolItem prototype,
            int createIfNotFoundWithSize,
            Action<PoolItem> onGet,
            Action<PoolItem> onReturn)
        {
            ObjectPool pool = null;
            if (createIfNotFoundWithSize > 0)
            {
                pool = new GameObject(prototype.name + "Pool").AddComponent<ObjectPool>();
                pool.Init(createIfNotFoundWithSize, prototype, onGet, onReturn);
                _poolsList.Add(pool);
            }

            return pool;
        }
    }
}