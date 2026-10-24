using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace CodexFramework.Utils.Pools
{
    public class PoolManager : Singleton<PoolManager>
    {
        public static event System.Action<ObjectPool> PoolCreated;
        public IEnumerable<ObjectPool> Pools => _poolsDict.Values;
        private readonly Dictionary<PoolItem, ObjectPool> _poolsDict = new();
        public PoolAsyncWorkQueue AsyncWork { get; } = new();
        private int _maxOperationsPerFrame = 8;
        private double _maxMillisecondsPerFrame = 2;
        private readonly CancellationTokenSource _transientRequests = new();
        public CancellationToken TransientRequestsToken => _transientRequests.Token;

        public void CancelTransientRequests()
        {
            _transientRequests.Cancel();
            foreach (var pool in _poolsDict.Values) pool.PruneCanceledAsyncWaiters();
        }

        public void ConfigureAsyncBudget(int maxOperations, double maxMilliseconds)
        {
            _maxOperationsPerFrame = maxOperations;
            _maxMillisecondsPerFrame = maxMilliseconds;
        }

        // Runs while gameplay ECS is paused too, so loading can finish its prewarm.
        private void Update() => AsyncWork.Process(Time.frameCount, _maxOperationsPerFrame, _maxMillisecondsPerFrame);

        protected override void Init()
        {
            base.Init();
            _poolsDict.Clear();
        }

        public ObjectPool GetByPrototype(IPoolableBehaviour prototype) => GetByPrototype(prototype.Item);
        public ObjectPool Prewarm(IPoolableBehaviour prototype) => Prewarm(prototype.Item);

        public ObjectPool Prewarm(PoolItem prototype)
        {
            var pool = GetByPrototype(prototype, prototype.PrewarmCount, prototype.MaxCount);
            pool.PrewarmWithBatchGrowth();
            return pool;
        }

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
                pool.transform.SetParent(transform, false);
                pool.UseAsyncBudget(AsyncWork);
                pool.Init(prototype, initialCount, maxCount);
                _poolsDict.Add(prototype, pool);
                PoolCreated?.Invoke(pool);
            }

            return pool;
        }
        
#if UNITY_EDITOR
        [SerializeField]
        private bool _logAllocated;

#endif
        private void OnDestroy()
        {
            CancelTransientRequests();
            foreach (var pool in _poolsDict.Values)
            {
#if UNITY_EDITOR
                if (_logAllocated && pool) Debug.Log($"{pool.Prototype.name}'s pool allocated: {pool.Allocated}");
#endif
                pool.CancelPendingRequests();
            }
            _transientRequests.Dispose();
            ClearSingletonInstance();
        }
    }
}
