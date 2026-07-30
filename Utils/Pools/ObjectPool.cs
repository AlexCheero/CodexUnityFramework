using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CodexFramework.Utils.Pools
{
    public class ObjectPool : MonoBehaviour
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
        
        public PoolItem Prototype => _prototype;
        public int Allocated => _items.Length;
        public IReadOnlyList<PoolItem> Items => _items;
        public int FirstAvailable => _firstAvailable;

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
                
                StartCoroutine(GrowRoutine(_growPerFrame));
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
                    Grow(_growPerFrame);
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
            item.transform.position = position;
            return item;
        }
        public PoolItem Get(Vector3 position, Quaternion rotation, bool forceGrow = true)
        {
            var item = Get(forceGrow);
            item.transform.SetPositionAndRotation(position, rotation);
            return item;
        }

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
                await GrowAsync(_growPerFrame, _firstAvailable + 1);
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

        private void Grow(int growPerFrame) => Grow(growPerFrame, _items.Length + 1);
        private void Grow(int growPerFrame, int minDesiredSize)
        {
#if UNITY_EDITOR
            var currentSize = _items?.Length ?? 0;
            if (minDesiredSize < currentSize)
                throw new Exception("minDesiredSize can't be smaller than _objects.Length");
#endif
            const int maxResizeDelta = 64;
            CodexECS.Utility.Utils.ResizeArray(minDesiredSize - 1, ref _items, maxResizeDelta);
            
            if (!_isGrowing)
                StartCoroutine(GrowRoutine(growPerFrame));
        }

        private async UniTask GrowAsync(int growPerFrame) => await GrowAsync(growPerFrame, _items.Length + 1);
        private async UniTask GrowAsync(int growPerFrame, int minDesiredSize)
        {
            Grow(growPerFrame, minDesiredSize);
            while (_isGrowing)
                await UniTask.Yield(PlayerLoopTiming.Update);
        }
        
        private bool _isGrowing;
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
        }
    }
}