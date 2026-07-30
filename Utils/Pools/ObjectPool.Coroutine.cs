#if !CODEX_UNITASK_SUPPORT
using System;
using System.Collections;
using UnityEngine;

namespace CodexFramework.Utils.Pools
{
    public partial class ObjectPool
    {
        partial void BeginInitialGrow()
        {
            if (!_isGrowing)
                StartCoroutine(GrowRoutine(_growPerFrame));
        }

        public void GetAsync(Action<PoolItem> onReady, bool forceGrow = true) =>
            StartCoroutine(GetAsyncRoutine(onReady, forceGrow));

        private IEnumerator GetAsyncRoutine(Action<PoolItem> onReady, bool forceGrow) =>
            GetAsyncCore(forceGrow, onReady);

        public void GetAsync<TState>(TState state, Action<PoolItem, TState> onReady, bool forceGrow = true) =>
            StartCoroutine(GetAsyncRoutine(state, onReady, forceGrow));

        private IEnumerator GetAsyncRoutine<TState>(TState state, Action<PoolItem, TState> onReady, bool forceGrow) =>
            GetAsyncCore(forceGrow, item => onReady?.Invoke(item, state));

        public void GetAsync(Vector3 position, Action<PoolItem> onReady, bool forceGrow = true) =>
            StartCoroutine(GetAsyncRoutine(position, onReady, forceGrow));

        private IEnumerator GetAsyncRoutine(Vector3 position, Action<PoolItem> onReady, bool forceGrow) =>
            GetAsyncCore(forceGrow, item =>
            {
                if (item != null)
                    item.transform.position = position;
                onReady?.Invoke(item);
            });

        public void GetAsync<TState>(Vector3 position, TState state, Action<PoolItem, TState> onReady, bool forceGrow = true) =>
            StartCoroutine(GetAsyncRoutine(position, state, onReady, forceGrow));

        private IEnumerator GetAsyncRoutine<TState>(Vector3 position, TState state, Action<PoolItem, TState> onReady, bool forceGrow) =>
            GetAsyncCore(forceGrow, item =>
            {
                if (item != null)
                    item.transform.position = position;
                onReady?.Invoke(item, state);
            });

        public void GetAsync(Vector3 position, Quaternion rotation, Action<PoolItem> onReady, bool forceGrow = true) =>
            StartCoroutine(GetAsyncRoutine(position, rotation, onReady, forceGrow));

        private IEnumerator GetAsyncRoutine(Vector3 position, Quaternion rotation, Action<PoolItem> onReady, bool forceGrow) =>
            GetAsyncCore(forceGrow, item =>
            {
                if (item != null)
                    item.transform.SetPositionAndRotation(position, rotation);
                onReady?.Invoke(item);
            });

        public void GetAsync<TState>(Vector3 position, Quaternion rotation, TState state, Action<PoolItem, TState> onReady, bool forceGrow = true) =>
            StartCoroutine(GetAsyncRoutine(position, rotation, state, onReady, forceGrow));

        private IEnumerator GetAsyncRoutine<TState>(
            Vector3 position,
            Quaternion rotation,
            TState state,
            Action<PoolItem, TState> onReady,
            bool forceGrow) =>
            GetAsyncCore(forceGrow, item =>
            {
                if (item != null)
                    item.transform.SetPositionAndRotation(position, rotation);
                onReady?.Invoke(item, state);
            });

        private IEnumerator GetAsyncCore(bool forceGrow, Action<PoolItem> onReady)
        {
#if DEBUG
            if (_firstAvailable > _items.Length)
                throw new Exception("_firstAvailable can't be bigger than _objects.Length");
#endif
            while (true)
            {
                if (_firstAvailable < _items.Length && _items[_firstAvailable] != null)
                {
                    var item = _items[_firstAvailable];
                    item.gameObject.SetActive(true);
                    _firstAvailable++;
                    item.OnGetFromPool();
                    onReady?.Invoke(item);
                    yield break;
                }

                if (_firstAvailable < _items.Length)
                {
                    if (!forceGrow)
                    {
                        onReady?.Invoke(null);
                        yield break;
                    }

#if UNITY_EDITOR
                    if (_maxCount > 0)
                        throw new Exception("can't grow fixed pool");
#endif
                    Grow(_growPerFrame, Math.Max(_firstAvailable + 1, _items.Length));
                    while (_firstAvailable < _items.Length && _items[_firstAvailable] == null)
                        yield return null;
                    continue;
                }

                if (_maxCount < 1)
                {
                    Grow(_growPerFrame);
                    while (_isGrowing)
                        yield return null;
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
                {
                    onReady?.Invoke(null);
                    yield break;
                }
            }
        }

        private void Grow(int growPerFrame, int minDesiredSize)
        {
            var currentSize = _items?.Length ?? 0;
            if (minDesiredSize < currentSize)
                minDesiredSize = currentSize;

            const int maxResizeDelta = 64;
            CodexECS.Utility.Utils.ResizeArray(minDesiredSize - 1, ref _items, maxResizeDelta);

            if (!_isGrowing)
                StartCoroutine(GrowRoutine(growPerFrame));
        }

        private IEnumerator GrowRoutine(int growPerFrame)
        {
            _isGrowing = true;

#if DEBUG
            if (growPerFrame < 1)
            {
                Debug.LogError("should add at least one object per frame");
                growPerFrame = 1;
            }
#endif

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
                    yield return null;
                }
            }

            _isGrowing = false;
        }
    }
}
#endif
