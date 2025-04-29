using System;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CodexFramework.Utils.Pools
{
    [ExecuteAlways]
    public class PooledRagdoll : PooledBehaviour, IResetOnGetPoolableBehaviour, IResetOnReturnPoolableBehaviour
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

#if UNITY_EDITOR
        [SerializeField, HideInInspector]
        private bool _initialized;
        
        private void OnValidate()
        {
            if (Application.isPlaying || _initialized)
                return;
            
            RecacheData();
        }
        
        [ContextMenu("Re-cache Data")]
        public void RecacheData()
        {
            Cache();
            SaveChanges();
        }
        
        private void SaveChanges()
        {
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.EditorUtility.SetDirty(gameObject);
            
            if (!UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this) && !Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            
            _initialized = true;
        }
        
        private void Cache()
        {
            SoftJointLimitCache SoftJointLimitToCache(SoftJointLimit limit) =>
                new()
                {
                    Limit = limit.limit,
                    Bounciness = limit.bounciness,
                    ContactDistance = limit.contactDistance
                };

            SoftJointLimitSpringCache SoftJointLimitSpringToCache(SoftJointLimitSpring spring) =>
                new()
                {
                    Spring = spring.spring,
                    Damper = spring.damper
                };

            _jointsCache = new();
            foreach (var joint in GetComponentsInChildren<CharacterJoint>(true))
            {
                _jointsCache.Add(new JointCache
                {
                    Joint = joint,
                    LowTwistLimit = SoftJointLimitToCache(joint.lowTwistLimit),
                    HighTwistLimit = SoftJointLimitToCache(joint.highTwistLimit),
                    Swing1Limit = SoftJointLimitToCache(joint.swing1Limit),
                    Swing2Limit = SoftJointLimitToCache(joint.swing2Limit),
                    SwingLimitSpring = SoftJointLimitSpringToCache(joint.swingLimitSpring),
                    Anchor = joint.anchor,
                    ConnectedAnchor = joint.connectedAnchor
                });
            }
            
            _rigidbodies = GetComponentsInChildren<Rigidbody>(true);
            _children = new();
            foreach (var childTransform in GetComponentsInChildren<Transform>(true))
            {
                _children.Add(new ChildTransform
                {
                    Transform = childTransform,
                    LocalPosition = childTransform.localPosition,
                    LocalRotation = childTransform.localRotation,
                });
            }
        }
        
        public bool Check() => CheckJoints() && CheckRigidbodies() && CheckChildren();

        private bool CheckJoints()
        {
            bool CompareTwistLimit(SoftJointLimit joint, SoftJointLimitCache cache)
            {
                if (!Mathf.Approximately(cache.Limit, joint.limit))
                    return false;
                if (!Mathf.Approximately(cache.Bounciness, joint.bounciness))
                    return false;
                if (!Mathf.Approximately(cache.ContactDistance, joint.contactDistance))
                    return false;
                return true;
            }

            bool CompareSwingLimitSpring(SoftJointLimitSpring joint, SoftJointLimitSpringCache cache)
            {
                if (!Mathf.Approximately(cache.Spring, joint.spring))
                    return false;
                if (!Mathf.Approximately(cache.Damper, joint.damper))
                    return false;
                return true;
            }
            
            _jointsCache ??= new();
            var joints = GetComponentsInChildren<CharacterJoint>(true);
            if (joints.Length != _jointsCache.Count)
                return false;
            for (var i = 0; i < joints.Length; i++)
            {
                var actualJoint = joints[i];
                var cachedJoint = _jointsCache[i];
                if (actualJoint != cachedJoint.Joint)
                    return false;
                if (!CompareTwistLimit(actualJoint.lowTwistLimit, cachedJoint.LowTwistLimit))
                    return false;
                if (!CompareTwistLimit(actualJoint.highTwistLimit, cachedJoint.HighTwistLimit))
                    return false;
                if (!CompareTwistLimit(actualJoint.swing1Limit, cachedJoint.Swing1Limit))
                    return false;
                if (!CompareTwistLimit(actualJoint.swing2Limit, cachedJoint.Swing2Limit))
                    return false;
                if (!CompareSwingLimitSpring(actualJoint.swingLimitSpring, cachedJoint.SwingLimitSpring))
                    return false;
                if (cachedJoint.Anchor != actualJoint.anchor)
                    return false;
                if (cachedJoint.ConnectedAnchor != actualJoint.connectedAnchor)
                    return false;
            }

            return true;
        }

        private bool CheckRigidbodies()
        {
            if (_rigidbodies == null)
                return false;
            var actualRigidbodies = GetComponentsInChildren<Rigidbody>(true);
            if (_rigidbodies.Length != actualRigidbodies.Length)
                return false;
            for (var i = 0; i < actualRigidbodies.Length; i++)
            {
                if (actualRigidbodies[i] != _rigidbodies[i])
                    return false;
            }
            
            return true;
        }

        private bool CheckChildren()
        {
            if (_children == null)
                return false;
            var actualChildren = GetComponentsInChildren<Transform>(true);
            if (_children.Count != actualChildren.Length)
                return false;
            for (var i = 0; i < actualChildren.Length; i++)
            {
                var cache = _children[i];
                var child = actualChildren[i];
                if (cache.Transform != child)
                    return false;
                if (cache.LocalPosition != child.localPosition)
                    return false;
                if (cache.LocalRotation != child.localRotation)
                    return false;
            }

            return true;
        }
#endif
        
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
            throw new System.NotImplementedException();
        }
    }
}