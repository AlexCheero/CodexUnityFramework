#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CodexFramework.Utils.Pools
{
    [ExecuteAlways]
    public partial class PooledRagdoll
    {
        [SerializeField, HideInInspector]
        private bool _initialized;

        public const string DismemberDummyName = "DismemberDummy";
        public static bool IsDismemberDummy(Component c) => c.name == DismemberDummyName;

        public bool ContainsDismemberDummy(Rigidbody rb)
        {
            for (var i = 0; i < _dismemberDummies.Length; i++)
            {
                if (_dismemberDummies[i] == rb)
                    return true;
            }
            return false;
        }

        public static int CountGameplayRigidbodies(GameObject root)
        {
            if (root == null)
                return 0;
            var rbs = root.GetComponentsInChildren<Rigidbody>(true);
            var count = 0;
            for (var i = 0; i < rbs.Length; i++)
            {
                if (rbs[i] != null && !IsDismemberDummy(rbs[i]))
                    count++;
            }
            return count;
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
                return;

            // AssetDatabase.LoadAssetAtPath returns a persistent prefab asset, whose hierarchy
            // Unity forbids us to mutate. Editable prefab contents and scene instances are
            // explicitly recached by their callers and remain non-persistent here.
            if (UnityEditor.EditorUtility.IsPersistent(this))
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
            CacheBoneRenderers();

            EnsureDismemberDummies();
            _rigidbodies = CollectRigidbodies();

            // Root pose is set by the pool; only cache child bones/parts.
            var transforms = GetComponentsInChildren<Transform>(true);
            var childCount = 0;
            for (var i = 1; i < transforms.Length; i++)
            {
                if (!IsDismemberDummy(transforms[i]))
                    childCount++;
            }
            _children = new ChildTransform[childCount];
            var dst = 0;
            for (var i = 1; i < transforms.Length; i++)
            {
                var childTransform = transforms[i];
                if (IsDismemberDummy(childTransform))
                    continue;
                _children[dst++] = new ChildTransform
                {
                    Transform = childTransform,
                    LocalPosition = childTransform.localPosition,
                    LocalRotation = childTransform.localRotation,
                };
            }
        }

        private void CacheBoneRenderers()
        {
            // A single-body character has no detachable joints to cache meshes on.
            if (_jointsCache.Length == 0)
                return;
            var renderers = GetComponentsInChildren<MeshRenderer>(true);
            var counts = new int[_jointsCache.Length];
            for (var i = 0; i < renderers.Length; i++)
                counts[GetRendererJointCacheIndex(renderers[i])]++;

            for (var i = 0; i < _jointsCache.Length; i++)
            {
                _jointsCache[i].Renderers = new MeshRenderer[counts[i]];
                counts[i] = 0;
            }

            for (var i = 0; i < renderers.Length; i++)
            {
                var cacheIndex = GetRendererJointCacheIndex(renderers[i]);
                _jointsCache[cacheIndex].Renderers[counts[cacheIndex]++] = renderers[i];
            }
        }

        private int GetRendererJointCacheIndex(MeshRenderer renderer)
        {
            var rigidbody = renderer.GetComponentInParent<Rigidbody>(true);
            for (var i = 0; i < _jointsCache.Length; i++)
            {
                if (_jointsCache[i].Joint.transform == rigidbody.transform)
                    return i;
            }
            for (var i = 0; i < _jointsCache.Length; i++)
            {
                if (_jointsCache[i].ConnectedBody == rigidbody)
                    return i;
            }
            throw new System.InvalidOperationException(
                $"MeshRenderer '{renderer.name}' is not owned by a cached ragdoll rigidbody.");
        }

        private void EnsureDismemberDummies()
        {
            var count = _jointsCache.Length;
            var kept = new HashSet<Rigidbody>();
            _dismemberDummies = new Rigidbody[count];
            for (var i = 0; i < count; i++)
            {
                var joint = _jointsCache[i].Joint;
                if (joint == null)
                    continue;
                var dummy = FindOrCreateDummy(joint);
                _dismemberDummies[i] = dummy;
                kept.Add(dummy);
            }

            var all = GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < all.Length; i++)
            {
                if (!IsDismemberDummy(all[i]))
                    continue;
                if (!all[i].TryGetComponent<Rigidbody>(out var rb) || !kept.Contains(rb))
                    Object.DestroyImmediate(all[i].gameObject);
            }
        }

        private static Rigidbody FindOrCreateDummy(CharacterJoint joint)
        {
            var parent = joint.transform;
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (!IsDismemberDummy(child) || !child.TryGetComponent<Rigidbody>(out var existing))
                    continue;
                ConfigureDummy(existing, joint);
                existing.Sleep();
                return existing;
            }

            var go = new GameObject(DismemberDummyName);
            go.transform.SetParent(parent, false);
            var rb = go.AddComponent<Rigidbody>();
            ConfigureDummy(rb, joint);
            rb.Sleep();
            return rb;
        }

        private static void ConfigureDummy(Rigidbody rb, CharacterJoint joint)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.detectCollisions = false;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.gameObject.layer = joint.gameObject.layer;
            rb.gameObject.SetActive(false);
            rb.transform.SetParent(joint.transform, false);
            rb.transform.localPosition = joint.anchor;
            rb.transform.localRotation = Quaternion.identity;
            rb.transform.localScale = Vector3.one;
        }

        private Rigidbody[] CollectRigidbodies()
        {
            var all = GetComponentsInChildren<Rigidbody>(true);
            var count = 0;
            for (var i = 0; i < all.Length; i++)
            {
                if (!IsDismemberDummy(all[i]))
                    count++;
            }
            var result = new Rigidbody[count];
            var dst = 0;
            for (var i = 0; i < all.Length; i++)
            {
                if (IsDismemberDummy(all[i]))
                    continue;
                result[dst++] = all[i];
            }
            return result;
        }
        
        public bool Check() =>
            CheckJoints() &&
            CheckRenderers() &&
            CheckDummies() &&
            CheckRigidbodies() &&
            CheckChildren();

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

        private bool CheckRenderers()
        {
            if (_jointsCache.Length == 0) return true;
            var cached = new HashSet<MeshRenderer>();
            for (var i = 0; i < _jointsCache.Length; i++)
            {
                var renderers = _jointsCache[i].Renderers;
                if (renderers == null)
                    return false;
                for (var j = 0; j < renderers.Length; j++)
                {
                    if (renderers[j] == null || !cached.Add(renderers[j]))
                        return false;
                }
            }

            var actual = GetComponentsInChildren<MeshRenderer>(true);
            if (cached.Count != actual.Length)
                return false;
            for (var i = 0; i < actual.Length; i++)
            {
                if (!cached.Contains(actual[i]))
                    return false;
            }
            return true;
        }

        private bool CheckDummies()
        {
            if (_jointsCache == null || _dismemberDummies == null ||
                _dismemberDummies.Length != _jointsCache.Length)
                return false;
            for (var i = 0; i < _jointsCache.Length; i++)
            {
                var joint = _jointsCache[i].Joint;
                var dummy = _dismemberDummies[i];
                if (joint == null || dummy == null)
                    return false;
                if (dummy.transform.parent != joint.transform)
                    return false;
            }
            return true;
        }

        private bool CheckRigidbodies()
        {
            if (_rigidbodies == null)
                return false;
            var actualRigidbodies = CollectRigidbodies();
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
            var expected = 0;
            for (var i = 1; i < actualChildren.Length; i++)
            {
                if (!IsDismemberDummy(actualChildren[i]))
                    expected++;
            }
            if (_children.Length != expected)
                return false;

            var dst = 0;
            for (var i = 1; i < actualChildren.Length; i++)
            {
                var child = actualChildren[i];
                if (IsDismemberDummy(child))
                    continue;
                var cache = _children[dst++];
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
