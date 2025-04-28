using UnityEngine;

namespace CodexFramework.Utils.Pools
{
    [RequireComponent(typeof(PoolItem))]
    public class PooledBehaviour : MonoBehaviour, IPoolableBehaviour
    {
        private PoolItem _item;
        //used instead of _item == null check because it is cheaper
        private bool _isItemInited;

        public PoolItem Item
        {
            get
            {
                if (_isItemInited)
                    return _item;
                _item = GetComponent<PoolItem>();
                _isItemInited = true;
                return _item;
            }
        }
    }
}