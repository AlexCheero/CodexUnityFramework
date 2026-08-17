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
        [SerializeField, HideInInspector]
        private Rigidbody[] _dismemberDummies;

        private const int DismemberMinJoints = 2;
        /// <summary>Upper bound of random breaks is this fraction of active joints (inclusive).</summary>
        private const float DismemberMaxJointFraction = 0.5f;

        private bool _pendingReturnReset;
        private readonly List<Rigidbody> _borrowedDismemberDummies = new(4);
        private static readonly List<int> DisconnectCandidates = new(32);

        public void OnGet()
        {
            _pendingReturnReset = false;
            // should be already called in OnReturn
            // RestoreBorrowedDummies();
            // DeactivateDummies();

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
            RestoreBorrowedDummies();
            DeactivateDummies();
            RestoreJointConnections();
            EnqueueReturnReset(this);
        }

        /// <summary>
        /// Breaks a random number of joints in [DismemberMinJoints, DismemberMaxJointFraction * count]
        /// (or all if fewer than DismemberMinJoints).
        /// Distal joints stay connected so a severed arm can still dangle.
        /// Disabled high-detail parts are not cut; the parent joint uses that part as dummy
        /// so the whole limb comes off. Broken joints stay active, retargeted to a dummy
        /// (null connectedBody would pin the piece to the world).
        /// </summary>
        public int DisconnectRandomJoints(List<DismemberedJoint> results, RagdollHighDetail[] highDetails)
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
                if (IsDisabledHighDetailPart(joint, highDetails))
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

                RetargetToDummy(jointIndex, joint, highDetails);

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

        private static bool IsDisabledHighDetailPart(CharacterJoint joint, RagdollHighDetail[] highDetails)
        {
            if (highDetails == null || highDetails.Length == 0)
                return false;
            if (!joint.TryGetComponent<Rigidbody>(out var rb))
                return false;
            for (var i = 0; i < highDetails.Length; i++)
            {
                if (highDetails[i].rigidbody != rb)
                    continue;
                return !rb.detectCollisions;
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

        private void RetargetToDummy(int jointIndex, CharacterJoint joint, RagdollHighDetail[] highDetails)
        {
            var dummy = ResolveDismemberDummy(jointIndex, joint, highDetails);
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

        private Rigidbody ResolveDismemberDummy(int jointIndex, CharacterJoint joint, RagdollHighDetail[] highDetails)
        {
            if (!joint.TryGetComponent<Rigidbody>(out var flyingRb))
                return _dismemberDummies[jointIndex];
            for (var i = 0; i < highDetails.Length; i++)
            {
                var part = highDetails[i];
                if (part.connectedBody != flyingRb)
                    continue;
                var rb = part.rigidbody;
                if (!rb.detectCollisions)
                    return rb;
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
