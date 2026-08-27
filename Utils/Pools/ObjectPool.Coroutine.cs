#if !CODEX_UNITASK_SUPPORT
using System;
using System.Collections;
using System.Threading;
using UnityEngine;

namespace CodexFramework.Utils.Pools
{
    public partial class ObjectPool
    {
        partial void BeginInitialGrow() => RequestGrow(_minimumCount);

        partial void StartGrowIfNeeded()
        {
            if (!_isGrowing && _allocatedCount < _growTarget)
                StartCoroutine(GrowRoutine(_growPerFrame));
        }

        public void GetAsync(Action<PoolItem> onReady, bool forceGrow = true) =>
            GetAsync(onReady, CancellationToken.None, forceGrow);

        public void GetAsync(
            Action<PoolItem> onReady,
            CancellationToken cancellationToken,
            bool forceGrow = true) =>
            EnqueueAsyncWaiter(onReady, forceGrow, cancellationToken);

        public void GetAsync<TState>(TState state, Action<PoolItem, TState> onReady, bool forceGrow = true) =>
            GetAsync(state, onReady, CancellationToken.None, forceGrow);

        public void GetAsync<TState>(
            TState state,
            Action<PoolItem, TState> onReady,
            CancellationToken cancellationToken,
            bool forceGrow = true) =>
            EnqueueAsyncWaiter(item => onReady?.Invoke(item, state), forceGrow, cancellationToken);

        public void GetAsync(Vector3 position, Action<PoolItem> onReady, bool forceGrow = true) =>
            GetAsync(position, onReady, CancellationToken.None, forceGrow);

        public void GetAsync(
            Vector3 position,
            Action<PoolItem> onReady,
            CancellationToken cancellationToken,
            bool forceGrow = true) =>
            EnqueueAsyncWaiter(item =>
            {
                if (item != null)
                    PlaceLease(item, position, false, default);
                onReady?.Invoke(item);
            }, forceGrow, cancellationToken);

        public void GetAsync<TState>(Vector3 position, TState state, Action<PoolItem, TState> onReady, bool forceGrow = true) =>
            GetAsync(position, state, onReady, CancellationToken.None, forceGrow);

        public void GetAsync<TState>(
            Vector3 position,
            TState state,
            Action<PoolItem, TState> onReady,
            CancellationToken cancellationToken,
            bool forceGrow = true) =>
            EnqueueAsyncWaiter(item =>
            {
                if (item != null)
                    PlaceLease(item, position, false, default);
                onReady?.Invoke(item, state);
            }, forceGrow, cancellationToken);

        public void GetAsync(Vector3 position, Quaternion rotation, Action<PoolItem> onReady, bool forceGrow = true) =>
            GetAsync(position, rotation, onReady, CancellationToken.None, forceGrow);

        public void GetAsync(
            Vector3 position,
            Quaternion rotation,
            Action<PoolItem> onReady,
            CancellationToken cancellationToken,
            bool forceGrow = true) =>
            EnqueueAsyncWaiter(item =>
            {
                if (item != null)
                    PlaceLease(item, position, true, rotation);
                onReady?.Invoke(item);
            }, forceGrow, cancellationToken);

        public void GetAsync<TState>(
            Vector3 position,
            Quaternion rotation,
            TState state,
            Action<PoolItem, TState> onReady,
            bool forceGrow = true) =>
            GetAsync(position, rotation, state, onReady, CancellationToken.None, forceGrow);

        public void GetAsync<TState>(
            Vector3 position,
            Quaternion rotation,
            TState state,
            Action<PoolItem, TState> onReady,
            CancellationToken cancellationToken,
            bool forceGrow = true) =>
            EnqueueAsyncWaiter(item =>
            {
                if (item != null)
                    PlaceLease(item, position, true, rotation);
                onReady?.Invoke(item, state);
            }, forceGrow, cancellationToken);

        private IEnumerator GrowRoutine(int growPerFrame)
        {
            if (_isGrowing)
                yield break;

            _isGrowing = true;
            var completedNormally = false;

#if DEBUG
            if (growPerFrame < 1)
            {
                Debug.LogError("should add at least one object per frame");
                growPerFrame = 1;
            }
#endif

            try
            {
                while (this && PrepareGrowBatch())
                {
                    var addedCount = 0;
                    while (addedCount < growPerFrame && TryGrowOne())
                        addedCount++;
                    if (addedCount == 0)
                        break;
                    // Keep the grow latch for the rest of this frame. Otherwise a same-frame
                    // request burst can restart this routine and spend the budget repeatedly.
                    yield return null;
                }
                completedNormally = true;
            }
            finally
            {
                if (this)
                {
                    _isGrowing = false;
                    if (completedNormally)
                        PrepareGrowBatch();
                    else
                        FailAllAsyncWaiters();
                }
            }
        }
    }
}
#endif
