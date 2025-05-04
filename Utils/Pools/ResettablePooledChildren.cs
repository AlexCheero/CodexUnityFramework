using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodexFramework.Utils.Pools
{
    public class ResettablePooledChildren : PooledBehaviour, IResetOnGetPoolableBehaviour
    {
        [Serializable]
#if UNITY_EDITOR
        public
#else
        private
#endif
        struct ChildTransform
        {
            public Transform Transform;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
        }

        [SerializeField]
#if UNITY_EDITOR
        public
#else
        private
# endif
        List<ChildTransform> _children;

        public void OnGet()
        {
            foreach (var childTransform in _children)
                childTransform.Transform.SetLocalPositionAndRotation(childTransform.LocalPosition, childTransform.LocalRotation);
        }
    }
}