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

        partial void BeginInitialGrow() => GrowAsync(_growPerFrame, _items.Length).Forget();

        public UniTask<PoolItem> GetAsync(bool forceGrow = true)
        {
            if (TryGet(out var item))
                return UniTask.FromResult(item);
            return GetAsyncCore(forceGrow);
        }

        private async UniTask<PoolItem> GetAsyncCore(bool forceGrow)
        {
#if DEBUG
            if (_firstAvailable > _items.Length)
                throw new Exception("_firstAvailable can't be bigger than _objects.Length");
#endif
            while (true)
            {
                if (TryGet(out var item))
                    return item;

                if (_firstAvailable < _items.Length)
                {
                    // Slot exists but not filled yet (async grow in progress).
                    if (!forceGrow)
                        return null;

#if UNITY_EDITOR
                    if (_maxCount > 0)
                        throw new Exception("can't grow fixed pool");
#endif
                    await GrowAsync(_growPerFrame, Math.Max(_firstAvailable + 1, _items.Length));
                    while (_firstAvailable < _items.Length && _items[_firstAvailable] == null)
                        await UniTask.Yield(PlayerLoopTiming.Update);
                    continue;
                }

                // Pool exhausted: grow or reclaim.
                if (_maxCount < 1)
                {
                    await GrowAsync(_growPerFrame, _items.Length + 1);
                    continue;
                }

                var reclaimed = false;
                for (var i = _items.Length - 1; i > -1; i--)
                {
                    var poolItem = _items[i];
                    if (poolItem == null || poolItem.IsInPool)
                        continue;
                    ReturnItem(poolItem);
                    reclaimed = true;
                    break;
                }

                if (!reclaimed)
                    return null;
            }
        }

        public UniTask<PoolItem> GetAsync(Vector3 position, bool forceGrow = true)
        {
            if (TryGet(out var item))
            {
                item.transform.position = position;
                return UniTask.FromResult(item);
            }

            var task = GetAsyncCore(forceGrow);
            var awaiter = task.GetAwaiter();
            if (awaiter.IsCompleted)
            {
                item = awaiter.GetResult();
                if (item != null)
                    item.transform.position = position;
                return UniTask.FromResult(item);
            }

            return ApplyPositionAsync(task, position);
        }

        public UniTask<PoolItem> GetAsync(Vector3 position, Quaternion rotation, bool forceGrow = true)
        {
            if (TryGet(out var item))
            {
                item.transform.SetPositionAndRotation(position, rotation);
                return UniTask.FromResult(item);
            }

            var task = GetAsyncCore(forceGrow);
            var awaiter = task.GetAwaiter();
            if (awaiter.IsCompleted)
            {
                item = awaiter.GetResult();
                if (item != null)
                    item.transform.SetPositionAndRotation(position, rotation);
                return UniTask.FromResult(item);
            }

            return ApplyPositionRotationAsync(task, position, rotation);
        }

        public void GetAsync(Action<PoolItem> onReady, bool forceGrow = true) =>
            GetAsync(onReady, InvokeActionCallback, forceGrow);

        public void GetAsync<TState>(TState state, Action<PoolItem, TState> onReady, bool forceGrow = true)
        {
            if (TryGet(out var item))
            {
                FinishGet(item, false, default, false, default, state, onReady);
                return;
            }

            CompleteOrContinue(GetAsyncCore(forceGrow), false, default, false, default, state, onReady);
        }

        public void GetAsync(Vector3 position, Action<PoolItem> onReady, bool forceGrow = true) =>
            GetAsync(position, onReady, InvokeActionCallback, forceGrow);

        public void GetAsync<TState>(Vector3 position, TState state, Action<PoolItem, TState> onReady, bool forceGrow = true)
        {
            if (TryGet(out var item))
            {
                FinishGet(item, true, position, false, default, state, onReady);
                return;
            }

            CompleteOrContinue(GetAsyncCore(forceGrow), true, position, false, default, state, onReady);
        }

        public void GetAsync(Vector3 position, Quaternion rotation, Action<PoolItem> onReady, bool forceGrow = true) =>
            GetAsync(position, rotation, onReady, InvokeActionCallback, forceGrow);

        public void GetAsync<TState>(Vector3 position, Quaternion rotation, TState state, Action<PoolItem, TState> onReady, bool forceGrow = true)
        {
            if (TryGet(out var item))
            {
                FinishGet(item, true, position, true, rotation, state, onReady);
                return;
            }

            CompleteOrContinue(GetAsyncCore(forceGrow), true, position, true, rotation, state, onReady);
        }

        private static void CompleteOrContinue<TState>(
            UniTask<PoolItem> task,
            bool hasPosition,
            Vector3 position,
            bool hasRotation,
            Quaternion rotation,
            TState state,
            Action<PoolItem, TState> onReady)
        {
            var awaiter = task.GetAwaiter();
            if (awaiter.IsCompleted)
            {
                FinishGet(awaiter.GetResult(), hasPosition, position, hasRotation, rotation, state, onReady);
                return;
            }

            var cont = new GetContinueState<TState>
            {
                Awaiter = awaiter,
                HasPosition = hasPosition,
                Position = position,
                HasRotation = hasRotation,
                Rotation = rotation,
                State = state,
                OnReady = onReady,
            };
            awaiter.SourceOnCompleted(GetContinueState<TState>.Invoke, cont);
        }

        private static void FinishGet<TState>(
            PoolItem item,
            bool hasPosition,
            Vector3 position,
            bool hasRotation,
            Quaternion rotation,
            TState state,
            Action<PoolItem, TState> onReady)
        {
            if (item != null)
            {
                if (hasRotation)
                    item.transform.SetPositionAndRotation(position, rotation);
                else if (hasPosition)
                    item.transform.position = position;
            }

            onReady?.Invoke(item, state);
        }

        private static async UniTask<PoolItem> ApplyPositionAsync(UniTask<PoolItem> task, Vector3 position)
        {
            var item = await task;
            if (item != null)
                item.transform.position = position;
            return item;
        }

        private static async UniTask<PoolItem> ApplyPositionRotationAsync(
            UniTask<PoolItem> task,
            Vector3 position,
            Quaternion rotation)
        {
            var item = await task;
            if (item != null)
                item.transform.SetPositionAndRotation(position, rotation);
            return item;
        }

        private void Grow(int growPerFrame, int minDesiredSize) =>
            GrowAsync(growPerFrame, minDesiredSize).Forget();

        private async UniTask GrowAsync(int growPerFrame, int minDesiredSize)
        {
            var currentSize = _items?.Length ?? 0;
            if (minDesiredSize < currentSize)
                minDesiredSize = currentSize;

            const int maxResizeDelta = 64;
            CodexECS.Utility.Utils.ResizeArray(minDesiredSize - 1, ref _items, maxResizeDelta);

            if (_isGrowing)
            {
                while (_isGrowing)
                    await UniTask.Yield(PlayerLoopTiming.Update);
                return;
            }

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
                for (int i = _firstAvailable; i < _items.Length; i++)
                {
                    //looks like it could cause problems if AddNew will be called outside of the routine
//#if DEBUG
//                if (_objects[i] != null)
//                    throw new Exception("non null pool items after grow");
//#endif
                    AddNew(i);
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
                _isGrowing = false;
            }
        }

        private sealed class GetContinueState<TState>
        {
            public UniTask<PoolItem>.Awaiter Awaiter;
            public bool HasPosition;
            public Vector3 Position;
            public bool HasRotation;
            public Quaternion Rotation;
            public TState State;
            public Action<PoolItem, TState> OnReady;

            public static readonly Action<object> Invoke = static obj =>
            {
                var state = (GetContinueState<TState>)obj;
                try
                {
                    FinishGet(
                        state.Awaiter.GetResult(),
                        state.HasPosition,
                        state.Position,
                        state.HasRotation,
                        state.Rotation,
                        state.State,
                        state.OnReady);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            };
        }
    }
}
#endif
