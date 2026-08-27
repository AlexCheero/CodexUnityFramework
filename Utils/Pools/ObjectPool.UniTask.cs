#if CODEX_UNITASK_SUPPORT
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CodexFramework.Utils.Pools
{
    public partial class ObjectPool
    {
        private static readonly Action<PoolItem, Action<PoolItem>> InvokeActionCallback =
            static (item, callback) => callback?.Invoke(item);

        partial void BeginInitialGrow() => RequestGrow(_minimumCount);

        partial void StartGrowIfNeeded()
        {
            if (!_isGrowing && _allocatedCount < _growTarget)
                GrowAsync(_growPerFrame).Forget();
        }

        public UniTask<PoolItem> GetAsync(bool forceGrow = true) =>
            GetAsync(CancellationToken.None, forceGrow);

        public UniTask<PoolItem> GetAsync(CancellationToken cancellationToken, bool forceGrow = true)
        {
            if (cancellationToken.IsCancellationRequested)
                return UniTask.FromCanceled<PoolItem>(cancellationToken);
            if (_isDestroying)
                return UniTask.FromResult<PoolItem>(null);
            if (_pendingAsyncCount == 0 && TryGet(out var item))
                return UniTask.FromResult(item);

            var tcs = new UniTaskCompletionSource<PoolItem>();
            EnqueueAsyncWaiter(
                result => tcs.TrySetResult(result),
                forceGrow,
                cancellationToken,
                () => tcs.TrySetCanceled(cancellationToken));
            return tcs.Task;
        }

        public UniTask<PoolItem> GetAsync(Vector3 position, bool forceGrow = true) =>
            GetAsync(position, CancellationToken.None, forceGrow);

        public UniTask<PoolItem> GetAsync(
            Vector3 position,
            CancellationToken cancellationToken,
            bool forceGrow = true)
        {
            if (cancellationToken.IsCancellationRequested)
                return UniTask.FromCanceled<PoolItem>(cancellationToken);
            if (_isDestroying)
                return UniTask.FromResult<PoolItem>(null);
            if (_pendingAsyncCount == 0 && TryGet(out var item))
            {
                try
                {
                    PlaceLease(item, position, false, default);
                    return UniTask.FromResult(item);
                }
                catch (Exception ex)
                {
                    return UniTask.FromException<PoolItem>(ex);
                }
            }

            var tcs = new UniTaskCompletionSource<PoolItem>();
            EnqueueAsyncWaiter(result =>
            {
                try
                {
                    if (result)
                        PlaceLease(result, position, false, default);
                    tcs.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }, forceGrow, cancellationToken, () => tcs.TrySetCanceled(cancellationToken));
            return tcs.Task;
        }

        public UniTask<PoolItem> GetAsync(Vector3 position, Quaternion rotation, bool forceGrow = true) =>
            GetAsync(position, rotation, CancellationToken.None, forceGrow);

        public UniTask<PoolItem> GetAsync(
            Vector3 position,
            Quaternion rotation,
            CancellationToken cancellationToken,
            bool forceGrow = true)
        {
            if (cancellationToken.IsCancellationRequested)
                return UniTask.FromCanceled<PoolItem>(cancellationToken);
            if (_isDestroying)
                return UniTask.FromResult<PoolItem>(null);
            if (_pendingAsyncCount == 0 && TryGet(out var item))
            {
                try
                {
                    PlaceLease(item, position, true, rotation);
                    return UniTask.FromResult(item);
                }
                catch (Exception ex)
                {
                    return UniTask.FromException<PoolItem>(ex);
                }
            }

            var tcs = new UniTaskCompletionSource<PoolItem>();
            EnqueueAsyncWaiter(result =>
            {
                try
                {
                    if (result)
                        PlaceLease(result, position, true, rotation);
                    tcs.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }, forceGrow, cancellationToken, () => tcs.TrySetCanceled(cancellationToken));
            return tcs.Task;
        }

        public void GetAsync(Action<PoolItem> onReady, bool forceGrow = true) =>
            GetAsync(onReady, CancellationToken.None, forceGrow);

        public void GetAsync(
            Action<PoolItem> onReady,
            CancellationToken cancellationToken,
            bool forceGrow = true) =>
            GetAsync(onReady, InvokeActionCallback, cancellationToken, forceGrow);

        public void GetAsync<TState>(TState state, Action<PoolItem, TState> onReady, bool forceGrow = true) =>
            GetAsync(state, onReady, CancellationToken.None, forceGrow);

        public void GetAsync<TState>(
            TState state,
            Action<PoolItem, TState> onReady,
            CancellationToken cancellationToken,
            bool forceGrow = true) =>
            EnqueueAsyncWaiter(
                item => FinishGet(item, false, default, false, default, state, onReady),
                forceGrow,
                cancellationToken);

        public void GetAsync(Vector3 position, Action<PoolItem> onReady, bool forceGrow = true) =>
            GetAsync(position, onReady, CancellationToken.None, forceGrow);

        public void GetAsync(
            Vector3 position,
            Action<PoolItem> onReady,
            CancellationToken cancellationToken,
            bool forceGrow = true) =>
            GetAsync(position, onReady, InvokeActionCallback, cancellationToken, forceGrow);

        public void GetAsync<TState>(Vector3 position, TState state, Action<PoolItem, TState> onReady, bool forceGrow = true) =>
            GetAsync(position, state, onReady, CancellationToken.None, forceGrow);

        public void GetAsync<TState>(
            Vector3 position,
            TState state,
            Action<PoolItem, TState> onReady,
            CancellationToken cancellationToken,
            bool forceGrow = true) =>
            EnqueueAsyncWaiter(
                item => FinishGet(item, true, position, false, default, state, onReady),
                forceGrow,
                cancellationToken);

        public void GetAsync(Vector3 position, Quaternion rotation, Action<PoolItem> onReady, bool forceGrow = true) =>
            GetAsync(position, rotation, onReady, CancellationToken.None, forceGrow);

        public void GetAsync(
            Vector3 position,
            Quaternion rotation,
            Action<PoolItem> onReady,
            CancellationToken cancellationToken,
            bool forceGrow = true) =>
            GetAsync(position, rotation, onReady, InvokeActionCallback, cancellationToken, forceGrow);

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
            EnqueueAsyncWaiter(
                item => FinishGet(item, true, position, true, rotation, state, onReady),
                forceGrow,
                cancellationToken);

        private void FinishGet<TState>(
            PoolItem item,
            bool hasPosition,
            Vector3 position,
            bool hasRotation,
            Quaternion rotation,
            TState state,
            Action<PoolItem, TState> onReady)
        {
            if (item && (hasPosition || hasRotation))
                PlaceLease(item, position, hasRotation, rotation);

            onReady?.Invoke(item, state);
        }

        private async UniTask GrowAsync(int growPerFrame)
        {
            if (!this || _isGrowing)
                return;

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
                    // request burst can restart this method and spend the budget repeatedly.
                    await UniTask.Yield(PlayerLoopTiming.Update);
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
