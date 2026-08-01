#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CodexFramework.Utils.Pools
{
    [ExecuteAlways]
    public partial class PooledRagdoll
    {
        [SerializeField, HideInInspector]
        private bool _initialized;
        
        private void OnValidate()
        {
            if (Application.isPlaying)
                return;

            if (_initialized && Check())
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
            var joints = GetComponentsInChildren<CharacterJoint>(true);
            _jointsCache = new JointCache[joints.Length];
            for (var i = 0; i < joints.Length; i++)
            {
                _jointsCache[i] = new JointCache
                {
                    Joint = joints[i],
                    ConnectedBody = joints[i].connectedBody
                };
            }
            
            _rigidbodies = GetComponentsInChildren<Rigidbody>(true);

            // Root pose is set by the pool; only cache child bones/parts.
            var transforms = GetComponentsInChildren<Transform>(true);
            _children = new ChildTransform[transforms.Length - 1];
            for (var i = 1; i < transforms.Length; i++)
            {
                var childTransform = transforms[i];
                _children[i - 1] = new ChildTransform
                {
                    Transform = childTransform,
                    LocalPosition = childTransform.localPosition,
                    LocalRotation = childTransform.localRotation,
                };
            }
        }
        
        public bool Check() => CheckJoints() && CheckRigidbodies() && CheckChildren();

        private bool CheckJoints()
        {
            if (_jointsCache == null)
                return false;

            var joints = GetComponentsInChildren<CharacterJoint>(true);
            if (joints.Length != _jointsCache.Length)
                return false;
            for (var i = 0; i < joints.Length; i++)
            {
                var actualJoint = joints[i];
                var cachedJoint = _jointsCache[i];
                if (actualJoint != cachedJoint.Joint)
                    return false;
                // connectedBody is toggled at runtime for high-detail parts
                if (!Application.isPlaying && cachedJoint.ConnectedBody != actualJoint.connectedBody)
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
            if (_children.Length != actualChildren.Length - 1)
                return false;

            for (var i = 0; i < _children.Length; i++)
            {
                var cache = _children[i];
                var child = actualChildren[i + 1];
                if (cache.Transform != child)
                    return false;
                if (cache.LocalPosition != child.localPosition)
                    return false;
                if (cache.LocalRotation != child.localRotation)
                    return false;
            }

            return true;
        }
    }
}
#endif