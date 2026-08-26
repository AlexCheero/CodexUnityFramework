using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CodexFramework.Utils.Pools
{
    public partial class PooledRagdoll : PooledBehaviour, IResetOnGetPoolableBehaviour, IResetOnReturnPoolableBehaviour
    {
        public struct DismemberedJoint
        {
            public Vector3 WorldPosition;
            public Vector3 Outward;
            public CharacterJoint Joint;
            public Rigidbody Connected;
        }

        public readonly struct DismembermentExclusion
        {
            public readonly Rigidbody Part;
            public readonly Rigidbody ConnectedBody;

            public DismembermentExclusion(Rigidbody part, Rigidbody connectedBody)
            {
                Part = part;
                ConnectedBody = connectedBody;
            }
        }

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
            public MeshRenderer[] Renderers;
        }

        [SerializeField, HideInInspector]
        private JointCache[] _jointsCache;
        [SerializeField, HideInInspector]
        private Rigidbody[] _rigidbodies;
        [SerializeField, HideInInspector]
        private ChildTransform[] _children;
        [SerializeField, HideInInspector]
        private Rigidbody[] _dismemberDummies;

        private const int DismemberMinJoints = 2;
        /// <summary>Upper bound of random breaks is this fraction of active joints (inclusive).</summary>
        private const float DismemberMaxJointFraction = 0.5f;

        private bool _pendingReturnReset;
        private readonly List<Rigidbody> _borrowedDismemberDummies = new(4);
        private static readonly List<int> DisconnectCandidates = new(32);
        private static readonly int RagdollDitherOpacityId = Shader.PropertyToID("_RagdollDitherOpacity");
        private static MaterialPropertyBlock VisualPropertyBlock;

        private Vector3 _pooledLocalScale;
        private bool _visualStateCached;

        private void Awake()
        {
            VisualPropertyBlock ??= new MaterialPropertyBlock();
            _pooledLocalScale = transform.localScale;
            _visualStateCached = true;
        }

        private void ResetVisualState()
        {
            if (!_visualStateCached)
                throw new InvalidOperationException("PooledRagdoll visual state was not initialized by Awake.");
            transform.localScale = _pooledLocalScale;
            for (var i = 0; i < _jointsCache.Length; i++)
            {
                var renderers = _jointsCache[i].Renderers;
                for (var j = 0; j < renderers.Length; j++)
                    ResetDither(renderers[j]);
            }
        }

        private static void ResetDither(MeshRenderer renderer)
        {
            renderer.GetPropertyBlock(VisualPropertyBlock);
            VisualPropertyBlock.SetFloat(RagdollDitherOpacityId, 1f);
            renderer.SetPropertyBlock(VisualPropertyBlock);
        }

        public void OnGet()
        {
            _pendingReturnReset = false;
            
            // should be already called in OnReturn
            // RestoreBorrowedDummies();
            // DeactivateDummies();
            // ResetVisualState();

            for (var i = 0; i < _children.Length; i++)
                _children[i].Reapply();

            // RestoreJointConnections();

            for (var i = 0; i < _rigidbodies.Length; i++)
            {
                var rb = _rigidbodies[i];
                // Non-kinematic before velocity — avoids "kinematic body" warnings.
                rb.isKinematic = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        public void OnReturn()
        {
            ResetVisualState();
            RestoreBorrowedDummies();
            DeactivateDummies();
            RestoreJointConnections();
            EnqueueReturnReset(this);
        }

        /// <summary>
        /// Breaks a random number of joints in [DismemberMinJoints, DismemberMaxJointFraction * count]
        /// (or all if fewer than DismemberMinJoints).
        /// Distal joints stay connected so a severed arm can still dangle.
        /// Excluded parts are not cut; the parent joint uses that part as dummy
        /// so the whole limb comes off. Broken joints stay active, retargeted to a dummy
        /// (null connectedBody would pin the piece to the world).
        /// </summary>
        public int DisconnectRandomJoints(
            List<DismemberedJoint> results,
            IReadOnlyList<DismembermentExclusion> exclusions)
        {
            results.Clear();
            DisconnectCandidates.Clear();
            for (var i = 0; i < _jointsCache.Length; i++)
            {
                if (i >= _dismemberDummies.Length)
                    break;
                var joint = _jointsCache[i].Joint;
                var connected = joint.connectedBody;
                if (connected == _dismemberDummies[i] || IsBorrowedDummy(connected))
                    continue;
                if (IsExcludedPart(joint, exclusions))
                    continue;
                DisconnectCandidates.Add(i);
            }

            var available = DisconnectCandidates.Count;
            if (available == 0)
                return 0;

            var maxBreaks = Mathf.Max(DismemberMinJoints, Mathf.FloorToInt(available * DismemberMaxJointFraction));
            var breakCount = available < DismemberMinJoints
                ? available
                : Random.Range(DismemberMinJoints, maxBreaks + 1);
            for (var n = 0; n < breakCount; n++)
            {
                var pick = Random.Range(n, available);
                (DisconnectCandidates[n], DisconnectCandidates[pick]) = (DisconnectCandidates[pick], DisconnectCandidates[n]);

                var jointIndex = DisconnectCandidates[n];
                var cache = _jointsCache[jointIndex];
                var joint = cache.Joint;
                var jointTr = joint.transform;
                var worldPos = jointTr.TransformPoint(joint.anchor);
                var connected = joint.connectedBody;
                var outward = jointTr.position - connected.position;
                if (outward.sqrMagnitude < 1e-8f)
                    outward = jointTr.up;
                else
                    outward.Normalize();

                RetargetToDummy(jointIndex, joint, exclusions);

                results.Add(new DismemberedJoint
                {
                    WorldPosition = worldPos,
                    Outward = outward,
                    Joint = joint,
                    Connected = connected
                });
            }

            return results.Count;
        }

        private static bool IsExcludedPart(
            CharacterJoint joint,
            IReadOnlyList<DismembermentExclusion> exclusions)
        {
            if (exclusions == null || exclusions.Count == 0)
                return false;
            if (!joint.TryGetComponent<Rigidbody>(out var rb))
                return false;
            for (var i = 0; i < exclusions.Count; i++)
            {
                if (exclusions[i].Part != rb)
                    continue;
                return true;
            }
            return false;
        }

        private bool IsBorrowedDummy(Rigidbody rb)
        {
            for (var i = 0; i < _borrowedDismemberDummies.Count; i++)
            {
                if (_borrowedDismemberDummies[i] == rb)
                    return true;
            }
            return false;
        }

        private void RestoreJointConnections()
        {
            for (var i = 0; i < _jointsCache.Length; i++)
            {
                var joint = _jointsCache[i].Joint;
                joint.autoConfigureConnectedAnchor = true;
                joint.connectedBody = null;
                joint.connectedBody = _jointsCache[i].ConnectedBody;
            }
        }

        private void RetargetToDummy(
            int jointIndex,
            CharacterJoint joint,
            IReadOnlyList<DismembermentExclusion> exclusions)
        {
            var dummy = ResolveDismemberDummy(jointIndex, joint, exclusions);
            dummy.gameObject.SetActive(true);
            dummy.isKinematic = false;
            if (dummy != _dismemberDummies[jointIndex])
            {
                dummy.detectCollisions = false;
                dummy.useGravity = false;
                RetargetPartJointToItsDummy(dummy);
                joint.autoConfigureConnectedAnchor = true;
                if (!IsBorrowedDummy(dummy))
                    _borrowedDismemberDummies.Add(dummy);
            }
            joint.connectedBody = dummy;
        }

        private void RetargetPartJointToItsDummy(Rigidbody part)
        {
            if (!part.TryGetComponent<CharacterJoint>(out var partJoint))
                return;
            for (var i = 0; i < _jointsCache.Length; i++)
            {
                if (_jointsCache[i].Joint != partJoint)
                    continue;
                var dedicated = _dismemberDummies[i];
                dedicated.gameObject.SetActive(true);
                dedicated.isKinematic = false;
                partJoint.connectedBody = dedicated;
                return;
            }
        }

        private Rigidbody ResolveDismemberDummy(
            int jointIndex,
            CharacterJoint joint,
            IReadOnlyList<DismembermentExclusion> exclusions)
        {
            if (!joint.TryGetComponent<Rigidbody>(out var flyingRb))
                return _dismemberDummies[jointIndex];
            for (var i = 0; i < exclusions.Count; i++)
            {
                var part = exclusions[i];
                if (part.ConnectedBody != flyingRb)
                    continue;
                return part.Part;
            }
            return _dismemberDummies[jointIndex];
        }

        private void RestoreBorrowedDummies()
        {
            for (var i = 0; i < _borrowedDismemberDummies.Count; i++)
                _borrowedDismemberDummies[i].useGravity = true;
            _borrowedDismemberDummies.Clear();
        }

        private void DeactivateDummies()
        {
            for (var i = 0; i < _dismemberDummies.Length; i++)
            {
                var rb = _dismemberDummies[i];
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                rb.isKinematic = true;
                rb.gameObject.SetActive(false);
            }
        }

        private void ResetRigidbodies()
        {
            for (var i = 0; i < _rigidbodies.Length; i++)
            {
                var rb = _rigidbodies[i];
                // High-detail parts may already be kinematic — don't write velocities on those.
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                rb.isKinematic = true;
            }
        }
    }
}
