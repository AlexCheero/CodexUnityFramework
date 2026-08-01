using System;
using UnityEngine;

namespace CodexFramework.Utils.Pools
{
    public partial class PooledRagdoll : PooledBehaviour, IResetOnGetPoolableBehaviour, IResetOnReturnPoolableBehaviour
    {
        [Serializable]
        private struct ChildTransform
        {
            public Transform Transform;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;

            public void Reapply() => Transform.SetLocalPositionAndRotation(LocalPosition, LocalRotation);
        }

        [Serializable]
        private struct JointCache
        {
            public CharacterJoint Joint;
            public Rigidbody ConnectedBody;
        }
    
        [SerializeField, HideInInspector]
        private JointCache[] _jointsCache;
        [SerializeField, HideInInspector]
        private Rigidbody[] _rigidbodies;
        [SerializeField, HideInInspector]
        private ChildTransform[] _children;

        public void OnGet()
        {
            for (var i = 0; i < _children.Length; i++)
                _children[i].Reapply();

            for (var i = 0; i < _jointsCache.Length; i++)
            {
                var joint = _jointsCache[i].Joint;
                joint.connectedBody = null;
                joint.connectedBody = _jointsCache[i].ConnectedBody;
            }

            for (var i = 0; i < _rigidbodies.Length; i++)
                _rigidbodies[i].isKinematic = false;
        }

        public void OnReturn()
        {
            for (var i = 0; i < _rigidbodies.Length; i++)
            {
                var rb = _rigidbodies[i];
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
        }
    }
}