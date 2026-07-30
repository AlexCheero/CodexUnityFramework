#if CODEX_UNITASK_SUPPORT
using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CodexFramework.Utils.Pools
{
    public partial class ObjectPool
    {
        partial void BeginInitialGrow() => GrowAsync(_growPerFrame, _items.Length).Forget();

        public async UniTask<PoolItem> GetAsync(bool forceGrow = true)
        {
#if DEBUG
            if (_firstAvailable > _items.Length)
                throw new Exception("_firstAvailable can't be bigger than _objects.Length");
#endif
            if (_firstAvailable == _items.Length)
            {
                if (_maxCount < 1)
                    await GrowAsync(_growPerFrame);
                else
                {
                    for (var i = _items.Length - 1; i > -1; i--)
                    {
                        var poolItem = _items[i];
                        if (poolItem == null || poolItem.IsInPool)
                            continue;
                        ReturnItem(poolItem);
                        break;
                    }
                }
            }

            if (_items[_firstAvailable] == null)
            {
                if (!forceGrow)
                    return null;

#if UNITY_EDITOR
                if (_maxCount > 0)
                    throw new Exception("can't grow fixed pool");
#endif
                // Array may already be larger (e.g. Init resized, grow still filling nulls).
                await GrowAsync(_growPerFrame, Math.Max(_firstAvailable + 1, _items.Length));
            }

            while (_firstAvailable < _items.Length && _items[_firstAvailable] == null)
                await UniTask.Yield(PlayerLoopTiming.Update);

            if (_firstAvailable >= _items.Length || _items[_firstAvailable] == null)
                return null;

            var item = _items[_firstAvailable];
            item.gameObject.SetActive(true);
            _firstAvailable++;

            item.OnGetFromPool();
            return item;
        }

        public void GetAsync(Action<PoolItem> onReady, bool forceGrow = true) =>
            GetAsyncInternal(onReady, forceGrow).Forget();

        private async UniTaskVoid GetAsyncInternal(Action<PoolItem> onReady, bool forceGrow)
        {
            var item = await GetAsync(forceGrow);
            onReady?.Invoke(item);
        }

        public void GetAsync<TState>(TState state, Action<PoolItem, TState> onReady, bool forceGrow = true) =>
            GetAsyncInternal(state, onReady, forceGrow).Forget();

        private async UniTaskVoid GetAsyncInternal<TState>(TState state, Action<PoolItem, TState> onReady, bool forceGrow)
        {
            var item = await GetAsync(forceGrow);
            onReady?.Invoke(item, state);
        }

        public async UniTask<PoolItem> GetAsync(Vector3 position, bool forceGrow = true)
        {
            var item = await GetAsync(forceGrow);
            if (item != null)
                item.transform.position = position;
            return item;
        }

        public void GetAsync(Vector3 position, Action<PoolItem> onReady, bool forceGrow = true) =>
            GetAsyncInternal(position, onReady, forceGrow).Forget();

        private async UniTaskVoid GetAsyncInternal(Vector3 position, Action<PoolItem> onReady, bool forceGrow)
        {
            var item = await GetAsync(position, forceGrow);
            onReady?.Invoke(item);
        }

        public void GetAsync<TState>(Vector3 position, TState state, Action<PoolItem, TState> onReady, bool forceGrow = true) =>
            GetAsyncInternal(position, state, onReady, forceGrow).Forget();

        private async UniTaskVoid GetAsyncInternal<TState>(Vector3 position, TState state, Action<PoolItem, TState> onReady, bool forceGrow)
        {
            var item = await GetAsync(position, forceGrow);
            onReady?.Invoke(item, state);
        }

        public async UniTask<PoolItem> GetAsync(Vector3 position, Quaternion rotation, bool forceGrow = true)
        {
            var item = await GetAsync(forceGrow);
            if (item != null)
                item.transform.SetPositionAndRotation(position, rotation);
            return item;
        }

        public void GetAsync(Vector3 position, Quaternion rotation, Action<PoolItem> onReady, bool forceGrow = true) =>
            GetAsyncInternal(position, rotation, onReady, forceGrow).Forget();

        private async UniTaskVoid GetAsyncInternal(Vector3 position, Quaternion rotation, Action<PoolItem> onReady, bool forceGrow)
        {
            var item = await GetAsync(position, rotation, forceGrow);
            onReady?.Invoke(item);
        }

        public void GetAsync<TState>(Vector3 position, Quaternion rotation, TState state, Action<PoolItem, TState> onReady, bool forceGrow = true) =>
            GetAsyncInternal(position, rotation, state, onReady, forceGrow).Forget();

        private async UniTaskVoid GetAsyncInternal<TState>(
            Vector3 position,
            Quaternion rotation,
            TState state,
            Action<PoolItem, TState> onReady,
            bool forceGrow)
        {
            var item = await GetAsync(position, rotation, forceGrow);
            onReady?.Invoke(item, state);
        }

        private void Grow(int growPerFrame, int minDesiredSize) =>
            GrowAsync(growPerFrame, minDesiredSize).Forget();

        private async UniTask GrowAsync(int growPerFrame) => await GrowAsync(growPerFrame, _items.Length + 1);

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
    }
}
#endif
