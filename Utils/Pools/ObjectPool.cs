using System;
using UnityEditor;
using UnityEngine;

namespace CodexFramework.Utils.Pools
{
    public class ObjectPool : MonoBehaviour
    {
        public event Action<PoolItem> OnGetItem;
        public event Action<PoolItem> OnReturnItem;

        [SerializeField]
        private int _initialCount;
        [SerializeField]
        private PoolItem _prototype;
        [SerializeField]
        private PoolItem[] _objects;
        private int _firstAvailable = 0;

        public GameObject PrototypeGO => _prototype.gameObject;

        public bool IsOnGetBinded => OnGetItem != null;
        public bool IsOnReturnBinded => OnReturnItem != null;

        public void Init(int initialCount, PoolItem prototype, Action<PoolItem> onGet = null, Action<PoolItem> onReturn = null)
        {
            _initialCount = initialCount;
            _prototype = prototype;
            Fix();

            if (onGet != null)
                OnGetItem += onGet;
            if (onReturn != null)
                OnReturnItem += onReturn;
        }

#if UNITY_EDITOR
        [MenuItem("Pools/Fix pools", false, -1)]
        private static void FixPools()
        {
            foreach (var pool in FindObjectsOfType<ObjectPool>())
                FixPool(pool);
        }

        private static void FixPool(ObjectPool pool)
        {
            pool.Fix();
            EditorUtility.SetDirty(pool);
        }
#endif

        private void Fix()
        {
            if (_initialCount == 0 || (_initialCount & _initialCount - 1) != 0)
                Debug.LogError("pool " + name + " size should be power of two");

            //make sure that there are will be only copies of prototype
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

        public PoolItem Get()
        {
#if DEBUG
            if (_firstAvailable > _objects.Length)
                throw new Exception("_firstAvailable can't be bigger than _objects.Length");
#endif
            if (_firstAvailable == _objects.Length)
                Grow();

            var item = _objects[_firstAvailable];
            item.gameObject.SetActive(true);
            _firstAvailable++;

            OnGetItem?.Invoke(item);

            return item;
        }
        public PoolItem Get(Vector3 position)
        {
            var retVal = Get();
            retVal.transform.position = position;
            return retVal;
        }
        public PoolItem Get(Vector3 position, Quaternion rotation)
        {
            var retVal = Get();
            retVal.transform.SetPositionAndRotation(position, rotation);
            return retVal;
        }

        private void Grow()
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

        private void AddNew(int idx)
        {
            _objects[idx] = Instantiate(_prototype, transform);
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

            OnReturnItem?.Invoke(item);

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