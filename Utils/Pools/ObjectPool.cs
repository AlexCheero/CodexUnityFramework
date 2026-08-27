using System;
using System.Collections.Generic;
using System.Threading;
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
        private int _allocatedCount;
        private int _minimumCount;
        private bool _isGrowing;
        private bool _isFulfillingAsyncWaiters;
        private bool _itemsDirty;
        private bool _isDestroying;
        private int _growTarget;
        private int _pendingAsyncCount;
        private int _cancellableAsyncCount;
        private readonly Queue<AsyncWaiter> _asyncWaiters = new();

        public PoolItem Prototype => _prototype;
        public int Allocated => _allocatedCount;
        public int Capacity => _items?.Length ?? 0;
        public int ActiveCount => _firstAvailable;
        public int AvailableCount => _allocatedCount - _firstAvailable;
        public int PendingAsyncCount => _pendingAsyncCount;
        public int GrowTarget => _growTarget;
        public IReadOnlyList<PoolItem> Items => _items ?? Array.Empty<PoolItem>();
        public int FirstAvailable => _firstAvailable;

        partial void BeginInitialGrow();
        partial void StartGrowIfNeeded();

        public bool TryGet(out PoolItem item)
        {
            if (_isDestroying)
            {
                item = null;
                return false;
            }

            RepairDestroyedItemsIfNeeded();
            if (_firstAvailable < _allocatedCount)
            {
                item = _items[_firstAvailable];
#if DEBUG
                if (!item)
                    throw new InvalidOperationException("allocated pool range contains a destroyed item");
#endif
                var leaseVersion = item.MarkCheckedOut();
                _firstAvailable++;
                try
                {
                    item.gameObject.SetActive(true);
                    ThrowIfLeaseWasReleased(item, leaseVersion);
                    item.InvokeOnGetCallbacks();
                    ThrowIfLeaseWasReleased(item, leaseVersion);
                    return true;
                }
                catch
                {
                    ReturnFailedCheckoutIfStillOwned(item, leaseVersion);
                    throw;
                }
            }

            item = null;
            return false;
        }

        public void Init(PoolItem prototype, int initialCount, int maxCount)
        {
            if (!prototype)
                throw new ArgumentNullException(nameof(prototype));
            if (initialCount < 1)
                throw new ArgumentOutOfRangeException(nameof(initialCount), initialCount,
                    "pool initial count must be positive");

            _prototype = prototype;
            _maxCount = maxCount;
            _growPerFrame = prototype.GrowPerFrame;
            if (_growPerFrame < 1)
            {
                Debug.LogError("should add at least one object per frame");
                _growPerFrame = 1;
            }
            _minimumCount = _maxCount > 0 ? _maxCount : initialCount;
            _firstAvailable = 0;
            _allocatedCount = 0;
            _growTarget = 0;
            _pendingAsyncCount = 0;
            _cancellableAsyncCount = 0;
            _itemsDirty = false;
            _isDestroying = false;
            EnsureCapacity(_minimumCount);

            if (_prototype.gameObject.scene.IsValid())
            {
                _items[0] = _prototype;
                _prototype.transform.SetParent(transform);
                _prototype.OnCreate();
                _prototype.AddToPool(this, 0);
                _prototype.gameObject.SetActive(false);
                _allocatedCount = 1;
            }

            BeginInitialGrow();
        }

        public PoolItem Get(bool forceGrow = true)
        {
            if (_isDestroying)
                return null;

#if DEBUG
            if (_firstAvailable > _allocatedCount)
                throw new Exception("active pool count can't be bigger than allocated count");
#endif
            if (TryGet(out var item))
                return item;

            if (_maxCount > 0)
            {
                if (_allocatedCount < _maxCount)
                {
                    if (!forceGrow)
                        return null;
                    AddNew(_allocatedCount);
                    return TryGet(out item) ? item : null;
                }

                // Reclaimed items are offered to async waiters first; keep reclaiming until
                // this sync get can take one or nothing is left to reclaim.
                while (TryReclaimOne())
                {
                    if (TryGet(out item))
                        return item;
                }
#if DEBUG
                Debug.LogError("fixed pool exhausted and nothing to reclaim: " + name);
#endif
                return null;
            }

            if (!forceGrow)
                return null;

#if UNITY_EDITOR
            if (_maxCount > 0)
                throw new Exception("can't grow fixed pool");
#endif
            RequestGrow(DesiredSizeForGets(1));
            if (TryGet(out item))
                return item;

            // Synchronous callers retain their historical force-grow behavior even when the
            // asynchronous grow batch has already spent this frame's budget.
            if (_allocatedCount < _growTarget)
                AddNew(_allocatedCount);
            return TryGet(out item) ? item : null;
        }

        public PoolItem Get(Vector3 position, bool forceGrow = true)
        {
            var item = Get(forceGrow);
            if (item)
                PlaceLease(item, position, false, default);
            return item;
        }

        public PoolItem Get(Vector3 position, Quaternion rotation, bool forceGrow = true)
        {
            var item = Get(forceGrow);
            if (item)
                PlaceLease(item, position, true, rotation);
            return item;
        }

        private void PlaceLease(
            PoolItem item,
            Vector3 position,
            bool hasRotation,
            Quaternion rotation)
        {
            var leaseVersion = item.LeaseVersion;
            try
            {
                item.gameObject.SetActive(false);
                ThrowIfLeaseWasReleased(item, leaseVersion);
                if (hasRotation)
                    item.transform.SetPositionAndRotation(position, rotation);
                else
                    item.transform.position = position;
                item.gameObject.SetActive(true);
                ThrowIfLeaseWasReleased(item, leaseVersion);
            }
            catch
            {
                ReturnFailedCheckoutIfStillOwned(item, leaseVersion);
                throw;
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

            EnsureCapacity(_maxCount);
            while (_allocatedCount < _maxCount)
                AddNew(_allocatedCount);
            RefreshGrowTarget();
        }

        private int DesiredSizeForGets(int additionalGets) =>
            _firstAvailable + _pendingAsyncCount + additionalGets;

        private void RequestGrow(int minDesiredSize)
        {
            if (_isDestroying)
                return;

            var desiredCount = Math.Max(_minimumCount, Math.Max(_allocatedCount, minDesiredSize));
            if (_maxCount > 0)
                desiredCount = Math.Min(desiredCount, _maxCount);
            _growTarget = desiredCount;
            EnsureCapacity(_growTarget);
            if (_allocatedCount < _growTarget)
                StartGrowIfNeeded();
        }

        private void RefreshGrowTarget() => RequestGrow(DesiredSizeForGets(0));

        private void EnsureCapacity(int requiredCount)
        {
            if (requiredCount <= Capacity)
                return;
            if (_maxCount > 0)
                requiredCount = Math.Min(requiredCount, _maxCount);

            const int maxResizeDelta = 64;
            CodexECS.Utility.Utils.ResizeArray(requiredCount - 1, ref _items, maxResizeDelta);
            if (_maxCount > 0 && _items.Length > _maxCount)
                Array.Resize(ref _items, _maxCount);
        }

        private bool TryReclaimOne()
        {
            RepairDestroyedItemsIfNeeded();
            for (var i = _firstAvailable - 1; i > -1; i--)
            {
                var poolItem = _items[i];
                if (!poolItem || poolItem.IsInPool)
                    continue;
                ReturnItem(poolItem);
                return true;
            }

            return false;
        }

        private void EnqueueAsyncWaiter(
            Action<PoolItem> onReady,
            bool forceGrow,
            CancellationToken cancellationToken = default,
            Action onCanceled = null)
        {
            if (onReady == null)
                throw new ArgumentNullException(nameof(onReady));
            if (cancellationToken.IsCancellationRequested)
            {
                InvokeCanceled(onCanceled);
                return;
            }
            if (_isDestroying)
            {
                onReady(null);
                return;
            }

            if (_pendingAsyncCount == 0 && TryGet(out var item))
            {
                CompleteImmediate(onReady, onCanceled, cancellationToken, item);
                return;
            }

            if (_maxCount > 0 && _allocatedCount >= _maxCount)
            {
                if (TryReclaimOne() && _pendingAsyncCount == 0 && TryGet(out item))
                {
                    CompleteImmediate(onReady, onCanceled, cancellationToken, item);
                    return;
                }

                onReady(null);
                return;
            }

            if (!forceGrow)
            {
                onReady(null);
                return;
            }

            var queuedWaiter = new AsyncWaiter(onReady, onCanceled, cancellationToken);
            _asyncWaiters.Enqueue(queuedWaiter);
            _pendingAsyncCount++;
            if (queuedWaiter.CanBeCanceled)
                _cancellableAsyncCount++;
            RequestGrow(DesiredSizeForGets(0));
            TryFulfillAsyncWaiters();
        }

        private void TryFulfillAsyncWaiters()
        {
            if (_isDestroying || _isFulfillingAsyncWaiters)
                return;

            _isFulfillingAsyncWaiters = true;
            try
            {
                while (_pendingAsyncCount > 0 && _asyncWaiters.Count > 0)
                {
                    var waiter = _asyncWaiters.Peek();
                    if (waiter.IsCancellationRequested)
                    {
                        _asyncWaiters.Dequeue();
                        RemovePendingWaiter(waiter);
                        InvokeCanceled(waiter);
                        continue;
                    }

                    if (!TryGet(out var item))
                    {
                        if (_maxCount > 0 && _allocatedCount >= _maxCount && TryReclaimOne())
                            continue;
                        break;
                    }

                    _asyncWaiters.Dequeue();
                    RemovePendingWaiter(waiter);
                    var deliveredLeaseVersion = item.LeaseVersion;
                    try
                    {
                        if (!waiter.TryComplete(item))
                            ReturnFailedDeliveryIfStillOwned(item, deliveredLeaseVersion);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                        ReturnFailedDeliveryIfStillOwned(item, deliveredLeaseVersion);
                    }
                }
            }
            finally
            {
                _isFulfillingAsyncWaiters = false;
                if (!_isDestroying)
                    RefreshGrowTarget();
            }
        }

        private void FailAllAsyncWaiters()
        {
            var waiters = _asyncWaiters.ToArray();
            _asyncWaiters.Clear();
            _pendingAsyncCount = 0;
            _cancellableAsyncCount = 0;
            for (var i = 0; i < waiters.Length; i++)
            {
                var waiter = waiters[i];
                try
                {
                    if (waiter.IsCancellationRequested)
                        waiter.Cancel();
                    else
                        waiter.Fail();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        private void PruneCanceledAsyncWaiters()
        {
            if (_cancellableAsyncCount == 0)
                return;

            List<AsyncWaiter> canceledWaiters = null;
            var queuedCount = _asyncWaiters.Count;
            for (var i = 0; i < queuedCount; i++)
            {
                var waiter = _asyncWaiters.Dequeue();
                if (!waiter.IsCancellationRequested)
                {
                    _asyncWaiters.Enqueue(waiter);
                    continue;
                }

                RemovePendingWaiter(waiter);
                canceledWaiters ??= new List<AsyncWaiter>();
                canceledWaiters.Add(waiter);
            }

            if (canceledWaiters == null)
                return;
            for (var i = 0; i < canceledWaiters.Count; i++)
                InvokeCanceled(canceledWaiters[i]);
        }

        private bool PrepareGrowBatch()
        {
            if (_isDestroying)
                return false;
            RepairDestroyedItemsIfNeeded();
            PruneCanceledAsyncWaiters();
            RefreshGrowTarget();
            return _allocatedCount < _growTarget;
        }

        private bool TryGrowOne()
        {
            if (_allocatedCount >= _growTarget)
                return false;
            EnsureCapacity(_growTarget);
            AddNew(_allocatedCount);
            TryFulfillAsyncWaiters();
            return true;
        }

        private void AddNew(int idx)
        {
            if (idx != _allocatedCount)
                throw new InvalidOperationException(
                    $"new pool item index {idx} does not match allocated count {_allocatedCount}");
            EnsureCapacity(idx + 1);
            if (_items[idx] != null)
                throw new InvalidOperationException($"pool slot {idx} is already occupied");

            _items[idx] = Instantiate(_prototype, transform);
            _items[idx].OnCreate();
            _items[idx].AddToPool(this, idx);
            _items[idx].gameObject.SetActive(false);
            _allocatedCount++;
        }

        //should be used only from PoolItem itself!
        public void ReturnItem(PoolItem item)
        {
            RepairDestroyedItemsIfNeeded();
            if (!item)
            {
                Debug.LogError("can't return a destroyed pool item", this);
                return;
            }
            if (item.Pool != this)
            {
                Debug.LogError($"can't return {item.name} to a pool that does not own it", item);
                return;
            }
            if (item.IsInPool)
            {
                Debug.LogError($"pool item {item.name} was returned twice", item);
                return;
            }
            if (item.Idx < 0 || item.Idx >= _firstAvailable || _items[item.Idx] != item)
            {
                Debug.LogError($"pool item {item.name} has an invalid active index {item.Idx}", item);
                return;
            }

            item.MarkReturning();
            try
            {
                item.gameObject.SetActive(false);
                item.transform.parent = transform;
                item.transform.position = Vector3.zero;
                item.transform.rotation = Quaternion.identity;
                item.InvokeOnReturnCallbacks();
            }
            finally
            {
                RepairDestroyedItemsIfNeeded();
                if (!_isDestroying && item && item.Pool == this && item.Idx >= 0 &&
                    item.Idx < _firstAvailable && _items[item.Idx] == item)
                    PublishReturnedItem(item);
                TryFulfillAsyncWaiters();
            }
        }

        private void PublishReturnedItem(PoolItem item)
        {
            _firstAvailable--;
            if (item.Idx >= _firstAvailable)
            {
                item.MarkReturned();
                return;
            }

            var returnedIndex = item.Idx;
            var temp = _items[_firstAvailable];
            _items[_firstAvailable] = item;
            _items[returnedIndex] = temp;
            temp.SetPoolIndex(this, returnedIndex);
            item.SetPoolIndex(this, _firstAvailable);
            item.MarkReturned();
        }

        internal void NotifyItemDestroyed()
        {
            if (!_isDestroying)
                _itemsDirty = true;
        }

        private void RepairDestroyedItemsIfNeeded()
        {
            if (!_itemsDirty || _items == null)
                return;
            _itemsDirty = false;

            var hasDestroyedItem = false;
            for (var i = 0; i < _allocatedCount; i++)
            {
                if (_items[i])
                    continue;
                hasDestroyedItem = true;
                break;
            }
            if (!hasDestroyedItem)
                return;

            var compacted = new PoolItem[_items.Length];
            var activeCount = 0;
            for (var i = 0; i < _allocatedCount; i++)
            {
                var poolItem = _items[i];
                if (!poolItem || poolItem.IsInPool && !poolItem.IsReturning)
                    continue;
                compacted[activeCount] = poolItem;
                poolItem.SetPoolIndex(this, activeCount);
                activeCount++;
            }

            var createdCount = activeCount;
            for (var i = 0; i < _allocatedCount; i++)
            {
                var poolItem = _items[i];
                if (!poolItem || !poolItem.IsInPool || poolItem.IsReturning)
                    continue;
                compacted[createdCount] = poolItem;
                poolItem.SetPoolIndex(this, createdCount);
                createdCount++;
            }

            _items = compacted;
            _firstAvailable = activeCount;
            _allocatedCount = createdCount;
        }

        private void CompleteImmediate(
            Action<PoolItem> onReady,
            Action onCanceled,
            CancellationToken cancellationToken,
            PoolItem item)
        {
            var deliveredLeaseVersion = item.LeaseVersion;
            if (cancellationToken.IsCancellationRequested)
            {
                ReturnFailedDeliveryIfStillOwned(item, deliveredLeaseVersion);
                InvokeCanceled(onCanceled);
                return;
            }

            try
            {
                onReady(item);
            }
            catch
            {
                ReturnFailedDeliveryIfStillOwned(item, deliveredLeaseVersion);
                throw;
            }
        }

        private bool OwnsLease(PoolItem item, int leaseVersion) =>
            item && item.Pool == this && item.LeaseVersion == leaseVersion && !item.IsInPool &&
            item.Idx >= 0 && item.Idx < _firstAvailable && item.Idx < _allocatedCount &&
            _items[item.Idx] == item;

        private void ThrowIfLeaseWasReleased(PoolItem item, int leaseVersion)
        {
            if (!OwnsLease(item, leaseVersion))
            {
                var itemName = item ? item.name : "<destroyed>";
                throw new InvalidOperationException(
                    $"pool item {itemName} was returned or transferred while it was being checked out");
            }
        }

        private void ReturnFailedCheckoutIfStillOwned(PoolItem item, int leaseVersion)
        {
            if (!OwnsLease(item, leaseVersion))
                return;
            try
            {
                ReturnItem(item);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private void ReturnFailedDeliveryIfStillOwned(PoolItem item, int leaseVersion)
        {
            if (!OwnsLease(item, leaseVersion))
                return;
            try
            {
                ReturnItem(item);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private void RemovePendingWaiter(AsyncWaiter waiter)
        {
            _pendingAsyncCount--;
            if (waiter.CanBeCanceled)
                _cancellableAsyncCount--;
        }

        private static void InvokeCanceled(AsyncWaiter waiter)
        {
            try
            {
                waiter.Cancel();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private static void InvokeCanceled(Action onCanceled)
        {
            try
            {
                onCanceled?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private void OnDestroy()
        {
            _isDestroying = true;
            FailAllAsyncWaiters();
        }

        private sealed class AsyncWaiter
        {
            private readonly Action<PoolItem> _onReady;
            private readonly Action _onCanceled;
            private readonly CancellationToken _cancellationToken;
            private bool _isCompleted;

            public bool IsCancellationRequested =>
                !_isCompleted && _cancellationToken.IsCancellationRequested;
            public bool CanBeCanceled => _cancellationToken.CanBeCanceled;

            public AsyncWaiter(
                Action<PoolItem> onReady,
                Action onCanceled,
                CancellationToken cancellationToken)
            {
                _onReady = onReady;
                _onCanceled = onCanceled;
                _cancellationToken = cancellationToken;
            }

            public bool TryComplete(PoolItem item)
            {
                if (_isCompleted)
                    return false;
                if (_cancellationToken.IsCancellationRequested)
                {
                    Cancel();
                    return false;
                }

                _isCompleted = true;
                _onReady(item);
                return true;
            }

            public void Cancel()
            {
                if (_isCompleted)
                    return;
                _isCompleted = true;
                _onCanceled?.Invoke();
            }

            public void Fail()
            {
                if (_isCompleted)
                    return;
                _isCompleted = true;
                _onReady(null);
            }
        }
    }
}
