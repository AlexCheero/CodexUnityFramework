using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodexFramework.Utils.Pools
{
    public class ResettablePooledChildren : PooledBehaviour, IResetOnGetPoolableBehaviour
    {
        [Serializable]
        private struct ChildTransform
        {
            public Transform Transform;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
        }

        [SerializeField]
        private List<ChildTransform> _children;

        public void OnGet()
        {
            foreach (var childTransform in _children)
                childTransform.Transform.SetLocalPositionAndRotation(childTransform.LocalPosition, childTransform.LocalRotation);
        }
        
#if UNITY_EDITOR
        [ContextMenu(nameof(Cache))]
        public void Cache()
        {
            _children ??= new();
            _children.Clear();
            foreach (var childTransform in gameObject.GetComponentsInChildren<Transform>())
            {
                if (childTransform == transform)
                    continue;
                _children.Add(new ChildTransform
                {
                    Transform = childTransform,
                    LocalPosition = childTransform.localPosition,
                    LocalRotation = childTransform.localRotation,
                });
            }
        }
#endif
    }
}