using System;
using System.Collections.Generic;
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
        private struct SoftJointLimitCache
        {
            public float Limit;
            public float Bounciness;
            public float ContactDistance;
        }
    
        [Serializable]
        private struct SoftJointLimitSpringCache
        {
            public float Spring;
            public float Damper;
        }
    
        [Serializable]
        private struct JointCache
        {
            public CharacterJoint Joint;
            public SoftJointLimitCache LowTwistLimit;
            public SoftJointLimitCache HighTwistLimit;
            public SoftJointLimitCache Swing1Limit;
            public SoftJointLimitCache Swing2Limit;
            public SoftJointLimitSpringCache SwingLimitSpring;
            public Vector3 Anchor;
            public Vector3 ConnectedAnchor;
        }
    
        [SerializeField, HideInInspector]
        private List<JointCache> _jointsCache;
        [SerializeField, HideInInspector]
        private Rigidbody[] _rigidbodies;
        [SerializeField, HideInInspector]
        private List<ChildTransform> _children;
        
        public void OnGet()
        {
            for (var i = 0; i < _children.Count; i++)
                _children[i].Reapply();
            
            SoftJointLimit ResetJointLimit(SoftJointLimit limit, SoftJointLimitCache cache)
            {
                limit.limit = cache.Limit;
                limit.bounciness = cache.Bounciness;
                limit.contactDistance = cache.ContactDistance;
                return limit;
            }
            
            SoftJointLimitSpring ResetJointLimitSpring(SoftJointLimitSpring spring, SoftJointLimitSpringCache cache)
            {
                spring.spring = cache.Spring;
                spring.damper = cache.Damper;
                return spring;
            }
            
            for (var i = 0; i < _jointsCache.Count; i++)
            {
                var jointCache = _jointsCache[i];
                var joint = jointCache.Joint;
                joint.lowTwistLimit = ResetJointLimit(joint.lowTwistLimit, jointCache.LowTwistLimit);
                joint.highTwistLimit = ResetJointLimit(joint.highTwistLimit, jointCache.HighTwistLimit);
                joint.swing1Limit = ResetJointLimit(joint.swing1Limit, jointCache.Swing1Limit);
                joint.swing2Limit = ResetJointLimit(joint.swing2Limit, jointCache.Swing2Limit);
                joint.swingLimitSpring = ResetJointLimitSpring(joint.swingLimitSpring, jointCache.SwingLimitSpring);
                joint.anchor = jointCache.Anchor;
                joint.connectedAnchor = jointCache.ConnectedAnchor;
                
                var temp = joint.connectedBody;
                joint.connectedBody = null;
                joint.connectedBody = temp;
            }

            for (var i = 0; i < _rigidbodies.Length; i++)
            {
                var rb = _rigidbodies[i];
                rb.velocity = rb.angularVelocity = Vector3.zero;
                rb.ResetInertiaTensor();
                rb.isKinematic = false;
                rb.WakeUp();
            }
        }

        public void OnReturn()
        {
            for (var i = 0; i < _rigidbodies.Length; i++)
            {
                var rb = _rigidbodies[i];
                rb.isKinematic = true;
                rb.Sleep();
            }
        }
    }
}