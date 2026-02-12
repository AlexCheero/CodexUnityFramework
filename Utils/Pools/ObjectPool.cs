using System;
using System.Collections;
using UnityEditor;
using UnityEngine;

namespace CodexFramework.Utils.Pools
{
    public class ObjectPool : MonoBehaviour
    {
        private const int GROW_PER_FRAME = 1;
        
        [SerializeField]
        private int _initialCount;
        [SerializeField]
        private PoolItem _prototype;
        [SerializeField]
        private PoolItem[] _objects;
        private int _firstAvailable = 0;
        
        public PoolItem Prototype => _prototype;

        public void Init(int initialCount, PoolItem prototype)
        {
            _prototype = prototype;
            Grow(GROW_PER_FRAME, initialCount);
        }

#if UNITY_EDITOR
        public int Allocated => _objects.Length;
        
        [MenuItem("Utils/Pools/Fix pools", false, -1)]
        private static void FixPools()
        {
            foreach (var pool in FindObjectsOfType<ObjectPool>())
                FixPool(pool);
        }

        private static void FixPool(ObjectPool pool)
        {
            pool.InstantFix();
            EditorUtility.SetDirty(pool);
        }
        
        private void InstantFix()
        {
            if (_initialCount == 0 || (_initialCount & _initialCount - 1) != 0)
                Debug.LogError("pool " + name + " size should be power of two");

            //make sure that there will be only copies of prototype
            var childCount = transform.childCount;
            for (int i = childCount - 1; i >= 0; i--)
            {
                var childObj = transform.GetChild(i).gameObject;
                DestroyImmediate(childObj);
            }

            Array.Resize(ref _objects, _initialCount);
            
            for (int i = 0; i < _initialCount; i++)
                AddNew(i);
        }
#endif

        public PoolItem Get(bool forceGrow = true)
        {
#if DEBUG
            if (_firstAvailable > _objects.Length)
                throw new Exception("_firstAvailable can't be bigger than _objects.Length");
#endif
            if (_firstAvailable == _objects.Length)
                Grow(GROW_PER_FRAME);

            if (_objects[_firstAvailable] == null)
            {
                if (!forceGrow)
                    return null;
                
                AddNew(_firstAvailable);
            }
            var item = _objects[_firstAvailable];
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

        private void InstantGrow()
        {
            var newLength = _objects.Length << 1;
            Array.Resize(ref _objects, newLength);

            for (int i = _firstAvailable; i < _objects.Length; i++)
            {
#if DEBUG
                if (_objects[i] != null)
                    throw new Exception("non null pool items after grow");
#endif
                AddNew(i);
            }
        }

        private void Grow(int growPerFrame) => Grow(growPerFrame, _objects.Length + 1);
        private void Grow(int growPerFrame, int minDesiredSize)
        {
#if UNITY_EDITOR
            var currentSize = _objects?.Length ?? 0;
            if (minDesiredSize < currentSize)
                throw new Exception("minDesiredSize can't be smaller than _objects.Length");
#endif
            const int maxResizeDelta = 64;
            CodexECS.Utility.Utils.ResizeArray(minDesiredSize, ref _objects, maxResizeDelta);
            
            if (!_isGrowing)
                StartCoroutine(GrowRoutine(growPerFrame));
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
            for (int i = _firstAvailable; i < _objects.Length; i++)
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
            if (_objects[idx] != null)
                return;

            _objects[idx] = Instantiate(_prototype, transform);
            _objects[idx].OnCreate();
            _objects[idx].AddToPool(this, idx);
            _objects[idx].gameObject.SetActive(false);
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
                var temp = _objects[_firstAvailable];
                _objects[_firstAvailable] = item;
                _objects[item.Idx] = temp;
                temp.AddToPool(this, item.Idx);
                item.AddToPool(this, _firstAvailable);
            }
        }
    }
}