using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodexFramework.Utils.Pools
{
    public partial class ObjectPool : MonoBehaviour
    {
        [SerializeField]
        private int _maxCount = -1;
        [SerializeField]
        private int _growPerFrame = 1;
        [SerializeField]
        private PoolItem _prototype;
        [SerializeField]
        private PoolItem[] _items;
        private int _firstAvailable = 0;
        private bool _isGrowing;
        private int _growTarget;
        private readonly Queue<Action<PoolItem>> _asyncWaiters = new();

        public PoolItem Prototype => _prototype;
        public int Allocated => _items.Length;
        public IReadOnlyList<PoolItem> Items => _items;
        public int FirstAvailable => _firstAvailable;

        partial void BeginInitialGrow();
        partial void StartGrowIfNeeded();

        public bool TryGet(out PoolItem item)
        {
            if (_firstAvailable < _items.Length && _items[_firstAvailable] != null)
            {
                item = _items[_firstAvailable];
                item.gameObject.SetActive(true);
                _firstAvailable++;
                item.OnGetFromPool();
                return true;
            }

            item = null;
            return false;
        }

        public void Init(PoolItem prototype, int initialCount, int maxCount)
        {
            _prototype = prototype;
            _maxCount = maxCount;
            _growPerFrame = prototype.GrowPerFrame;
#if DEBUG
            if (_growPerFrame < 1)
            {
                Debug.LogError("should add at least one object per frame");
                _growPerFrame = 1;
            }
#endif
            var count = _maxCount > 0 ? _maxCount : initialCount;

            if (_prototype.gameObject.scene.IsValid())
            {
                const int maxResizeDelta = 64;
                CodexECS.Utility.Utils.ResizeArray(count - 1, ref _items, maxResizeDelta);

                _items[0] = _prototype;
                _prototype.transform.SetParent(transform);
                _prototype.OnCreate();
                _prototype.AddToPool(this, 0);
                _prototype.gameObject.SetActive(false);

                BeginInitialGrow();
            }
            else
            {
                Grow(_growPerFrame, count);
            }
        }

        public PoolItem Get(bool forceGrow = true)
        {
#if DEBUG
            if (_firstAvailable > _items.Length)
                throw new Exception("_firstAvailable can't be bigger than _objects.Length");
#endif
            if (_firstAvailable == _items.Length)
            {
                if (_maxCount < 1)
                    Grow(_growPerFrame, DesiredSizeForGets(1));
                else
                {
                    // Reclaimed items are offered to async waiters first; keep reclaiming until
                    // this sync get can take one or nothing is left to reclaim.
                    while (_firstAvailable == _items.Length && TryReclaimOne())
                    {
                    }

                    if (_firstAvailable == _items.Length)
                    {
#if DEBUG
                        Debug.LogError("fixed pool exhausted and nothing to reclaim: " + name);
#endif
                        return null;
                    }
                }
            }

            if (_firstAvailable >= _items.Length || _items[_firstAvailable] == null)
            {
                if (!forceGrow)
                    return null;

#if UNITY_EDITOR
                if (_maxCount > 0)
                    throw new Exception("can't grow fixed pool");
#endif

                if (_firstAvailable >= _items.Length)
                    RequestGrow(DesiredSizeForGets(1));

                if (_firstAvailable >= _items.Length)
                    return null;

                AddNew(_firstAvailable);
            }

            var item = _items[_firstAvailable];
            item.gameObject.SetActive(true);
            _firstAvailable++;

            item.OnGetFromPool();
            return item;
        }

        public PoolItem Get(Vector3 position, bool forceGrow = true)
        {
            var item = Get(forceGrow);
            if (item)
                item.transform.position = position;
            return item;
        }

        public PoolItem Get(Vector3 position, Quaternion rotation, bool forceGrow = true)
        {
            var item = Get(forceGrow);
            if (item)
                item.transform.SetPositionAndRotation(position, rotation);
            return item;
        }

        private void InstantGrow()
        {
            var newLength = _items.Length << 1;
            Array.Resize(ref _items, newLength);

            for (int i = _firstAvailable; i < _items.Length; i++)
            {
#if DEBUG
                if (_items[i] != null)
                    throw new Exception("non null pool items after grow");
#endif
                AddNew(i);
            }
        }

        public void AllocateAll()
        {
            if (_maxCount < 1)
            {
#if DEBUG
                Debug.LogError("AllocateAll is only for fixed pools");
#endif
                return;
            }

            const int maxResizeDelta = 64;
            CodexECS.Utility.Utils.ResizeArray(_maxCount - 1, ref _items, maxResizeDelta);

            for (int i = 0; i < _items.Length; i++)
                AddNew(i);
        }

        private void Grow(int growPerFrame) => Grow(growPerFrame, DesiredSizeForGets(1));

        private void Grow(int growPerFrame, int minDesiredSize) => RequestGrow(minDesiredSize);

        private int DesiredSizeForGets(int additionalGets) =>
            _firstAvailable + _asyncWaiters.Count + additionalGets;

        private void RequestGrow(int minDesiredSize)
        {
            var currentSize = _items?.Length ?? 0;
            if (minDesiredSize < currentSize)
                minDesiredSize = currentSize;
            if (minDesiredSize < 1)
                minDesiredSize = 1;

            if (minDesiredSize > _growTarget)
                _growTarget = minDesiredSize;

            const int maxResizeDelta = 64;
            CodexECS.Utility.Utils.ResizeArray(_growTarget - 1, ref _items, maxResizeDelta);

            StartGrowIfNeeded();
        }

        private bool TryReclaimOne()
        {
            for (var i = _items.Length - 1; i > -1; i--)
            {
                var poolItem = _items[i];
                if (poolItem == null || poolItem.IsInPool)
                    continue;
                ReturnItem(poolItem);
                return true;
            }

            return false;
        }

        private void EnqueueAsyncWaiter(Action<PoolItem> onReady, bool forceGrow)
        {
            if (_asyncWaiters.Count == 0 && TryGet(out var item))
            {
                onReady?.Invoke(item);
                return;
            }

            if (_maxCount > 0)
            {
                if (TryReclaimOne() && _asyncWaiters.Count == 0 && TryGet(out item))
                {
                    onReady?.Invoke(item);
                    return;
                }

                onReady?.Invoke(null);
                return;
            }

            if (!forceGrow)
            {
                onReady?.Invoke(null);
                return;
            }

            _asyncWaiters.Enqueue(onReady);
            RequestGrow(DesiredSizeForGets(0));
            TryFulfillAsyncWaiters();
        }

        private void TryFulfillAsyncWaiters()
        {
            while (_asyncWaiters.Count > 0 && TryGet(out var item))
            {
                var onReady = _asyncWaiters.Dequeue();
                try
                {
                    onReady?.Invoke(item);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        private void FailAllAsyncWaiters()
        {
            while (_asyncWaiters.Count > 0)
            {
                var onReady = _asyncWaiters.Dequeue();
                try
                {
                    onReady?.Invoke(null);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        private bool HasUnfilledSlots()
        {
            if (_items == null)
                return false;
            for (int i = _firstAvailable; i < _items.Length; i++)
            {
                if (_items[i] == null)
                    return true;
            }

            return false;
        }

        private void AddNew(int idx)
        {
            if (_items[idx] != null)
                return;

            _items[idx] = Instantiate(_prototype, transform);
            _items[idx].OnCreate();
            _items[idx].AddToPool(this, idx);
            _items[idx].gameObject.SetActive(false);
        }

        //should be used only from PoolItem itself!
        public void ReturnItem(PoolItem item)
        {
#if DEBUG
            if (_firstAvailable == 0)
                throw new Exception("pool have no active items but something is returned: " + item.name);
#endif

            item.gameObject.SetActive(false);
            item.transform.parent = transform;
            item.transform.position = Vector3.zero;
            item.transform.rotation = Quaternion.identity;

            _firstAvailable--;
            if (item.Idx < _firstAvailable)
            {
                var temp = _items[_firstAvailable];
                _items[_firstAvailable] = item;
                _items[item.Idx] = temp;
                temp.AddToPool(this, item.Idx);
                item.AddToPool(this, _firstAvailable);
            }

            item.OnReturn();
            TryFulfillAsyncWaiters();
        }

        private void OnDestroy() => FailAllAsyncWaiters();
    }
}
