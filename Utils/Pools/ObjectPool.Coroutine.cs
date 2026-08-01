#if !CODEX_UNITASK_SUPPORT
using System;
using System.Collections;
using UnityEngine;

namespace CodexFramework.Utils.Pools
{
    public partial class ObjectPool
    {
        partial void BeginInitialGrow() => RequestGrow(_items.Length);

        partial void StartGrowIfNeeded()
        {
            if (!_isGrowing)
                StartCoroutine(GrowRoutine(_growPerFrame));
        }

        public void GetAsync(Action<PoolItem> onReady, bool forceGrow = true) =>
            EnqueueAsyncWaiter(onReady, forceGrow);

        public void GetAsync<TState>(TState state, Action<PoolItem, TState> onReady, bool forceGrow = true) =>
            EnqueueAsyncWaiter(item => onReady?.Invoke(item, state), forceGrow);

        public void GetAsync(Vector3 position, Action<PoolItem> onReady, bool forceGrow = true) =>
            EnqueueAsyncWaiter(item =>
            {
                if (item != null)
                    item.transform.position = position;
                onReady?.Invoke(item);
            }, forceGrow);

        public void GetAsync<TState>(Vector3 position, TState state, Action<PoolItem, TState> onReady, bool forceGrow = true) =>
            EnqueueAsyncWaiter(item =>
            {
                if (item != null)
                    item.transform.position = position;
                onReady?.Invoke(item, state);
            }, forceGrow);

        public void GetAsync(Vector3 position, Quaternion rotation, Action<PoolItem> onReady, bool forceGrow = true) =>
            EnqueueAsyncWaiter(item =>
            {
                if (item != null)
                    item.transform.SetPositionAndRotation(position, rotation);
                onReady?.Invoke(item);
            }, forceGrow);

        public void GetAsync<TState>(
            Vector3 position,
            Quaternion rotation,
            TState state,
            Action<PoolItem, TState> onReady,
            bool forceGrow = true) =>
            EnqueueAsyncWaiter(item =>
            {
                if (item != null)
                    item.transform.SetPositionAndRotation(position, rotation);
                onReady?.Invoke(item, state);
            }, forceGrow);

        private IEnumerator GrowRoutine(int growPerFrame)
        {
            if (_isGrowing)
                yield break;

            _isGrowing = true;

#if DEBUG
            if (growPerFrame < 1)
            {
                Debug.LogError("should add at least one object per frame");
                growPerFrame = 1;
            }
#endif

            var addThisFrame = growPerFrame;
            while (this)
            {
                if (_growTarget > (_items?.Length ?? 0))
                {
                    const int maxResizeDelta = 64;
                    CodexECS.Utility.Utils.ResizeArray(_growTarget - 1, ref _items, maxResizeDelta);
                }

                var fillIdx = -1;
                for (int i = _firstAvailable; i < _items.Length; i++)
                {
                    if (_items[i] == null)
                    {
                        fillIdx = i;
                        break;
                    }
                }

                if (fillIdx < 0)
                {
                    if ((_items?.Length ?? 0) >= _growTarget)
                        break;

                    const int maxResizeDelta = 64;
                    CodexECS.Utility.Utils.ResizeArray(Math.Max(_growTarget, 1) - 1, ref _items, maxResizeDelta);
                    continue;
                }

                AddNew(fillIdx);
                TryFulfillAsyncWaiters();

                addThisFrame--;
                if (addThisFrame == 0)
                {
                    addThisFrame = growPerFrame;
                    yield return null;
                }
            }

            if (this)
            {
                _isGrowing = false;
                if (_asyncWaiters.Count > 0 || HasUnfilledSlots())
                    RequestGrow(DesiredSizeForGets(0));
                else
                    _growTarget = _items?.Length ?? 0;
            }
        }
    }
}
#endif
