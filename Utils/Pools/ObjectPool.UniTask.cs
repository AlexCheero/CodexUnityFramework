#if CODEX_UNITASK_SUPPORT
using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CodexFramework.Utils.Pools
{
    public partial class ObjectPool
    {
        private static readonly Action<PoolItem, Action<PoolItem>> InvokeActionCallback =
            static (item, callback) => callback?.Invoke(item);

        partial void BeginInitialGrow() => RequestGrow(_items.Length);

        partial void StartGrowIfNeeded()
        {
            if (!_isGrowing)
                GrowAsync(_growPerFrame).Forget();
        }

        public UniTask<PoolItem> GetAsync(bool forceGrow = true)
        {
            if (_asyncWaiters.Count == 0 && TryGet(out var item))
                return UniTask.FromResult(item);

            var tcs = new UniTaskCompletionSource<PoolItem>();
            EnqueueAsyncWaiter(result => tcs.TrySetResult(result), forceGrow);
            return tcs.Task;
        }

        public UniTask<PoolItem> GetAsync(Vector3 position, bool forceGrow = true)
        {
            if (_asyncWaiters.Count == 0 && TryGet(out var item))
            {
                item.gameObject.SetActive(false);
                item.transform.position = position;
                item.gameObject.SetActive(true);
                return UniTask.FromResult(item);
            }

            var tcs = new UniTaskCompletionSource<PoolItem>();
            EnqueueAsyncWaiter(result =>
            {
                if (result)
                {
                    result.gameObject.SetActive(false);
                    result.transform.position = position;
                    result.gameObject.SetActive(true);
                }
                tcs.TrySetResult(result);
            }, forceGrow);
            return tcs.Task;
        }

        public UniTask<PoolItem> GetAsync(Vector3 position, Quaternion rotation, bool forceGrow = true)
        {
            if (_asyncWaiters.Count == 0 && TryGet(out var item))
            {
                item.gameObject.SetActive(false);
                item.transform.SetPositionAndRotation(position, rotation);
                item.gameObject.SetActive(true);
                return UniTask.FromResult(item);
            }

            var tcs = new UniTaskCompletionSource<PoolItem>();
            EnqueueAsyncWaiter(result =>
            {
                if (result)
                {
                    result.gameObject.SetActive(false);
                    result.transform.SetPositionAndRotation(position, rotation);
                    result.gameObject.SetActive(true);
                }
                tcs.TrySetResult(result);
            }, forceGrow);
            return tcs.Task;
        }

        public void GetAsync(Action<PoolItem> onReady, bool forceGrow = true) =>
            GetAsync(onReady, InvokeActionCallback, forceGrow);

        public void GetAsync<TState>(TState state, Action<PoolItem, TState> onReady, bool forceGrow = true) =>
            EnqueueAsyncWaiter(item => FinishGet(item, false, default, false, default, state, onReady), forceGrow);

        public void GetAsync(Vector3 position, Action<PoolItem> onReady, bool forceGrow = true) =>
            GetAsync(position, onReady, InvokeActionCallback, forceGrow);

        public void GetAsync<TState>(Vector3 position, TState state, Action<PoolItem, TState> onReady, bool forceGrow = true) =>
            EnqueueAsyncWaiter(item => FinishGet(item, true, position, false, default, state, onReady), forceGrow);

        public void GetAsync(Vector3 position, Quaternion rotation, Action<PoolItem> onReady, bool forceGrow = true) =>
            GetAsync(position, rotation, onReady, InvokeActionCallback, forceGrow);

        public void GetAsync<TState>(
            Vector3 position,
            Quaternion rotation,
            TState state,
            Action<PoolItem, TState> onReady,
            bool forceGrow = true) =>
            EnqueueAsyncWaiter(item => FinishGet(item, true, position, true, rotation, state, onReady), forceGrow);

        private static void FinishGet<TState>(
            PoolItem item,
            bool hasPosition,
            Vector3 position,
            bool hasRotation,
            Quaternion rotation,
            TState state,
            Action<PoolItem, TState> onReady)
        {
            if (item && (hasPosition || hasRotation))
            {
                // Place while inactive so TrailRenderers don't record a streak from the pool origin.
                item.gameObject.SetActive(false);
                if (hasRotation)
                    item.transform.SetPositionAndRotation(position, rotation);
                else
                    item.transform.position = position;
                item.gameObject.SetActive(true);
            }

            onReady?.Invoke(item, state);
        }

        private async UniTask GrowAsync(int growPerFrame)
        {
            if (!this || _isGrowing)
                return;

            _isGrowing = true;

#if DEBUG
            if (growPerFrame < 1)
            {
                Debug.LogError("should add at least one object per frame");
                growPerFrame = 1;
            }
#endif

            try
            {
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
                        await UniTask.Yield(PlayerLoopTiming.Update);
                    }
                }
            }
            finally
            {
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
}
#endif
