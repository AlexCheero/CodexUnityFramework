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
        private static readonly List<int> DisconnectCandidates = new(32);

        public void OnGet()
        {
            _pendingReturnReset = false;
            DeactivateDummies();

            for (var i = 0; i < _children.Length; i++)
                _children[i].Reapply();

            RestoreJointConnections();

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
            DeactivateDummies();
            RestoreJointConnections();
            EnqueueReturnReset(this);
        }

        /// <summary>
        /// Breaks a random number of joints in [DismemberMinJoints, DismemberMaxJointFraction * count]
        /// (or all if fewer than DismemberMinJoints).
        /// Distal joints stay connected so a severed arm can still dangle.
        /// Broken joints stay active, retargeted to a dummy on the distal part
        /// (null connectedBody would pin the piece to the world).
        /// </summary>
        public int DisconnectRandomJoints(List<DismemberedJoint> results)
        {
            results.Clear();
            DisconnectCandidates.Clear();
            for (var i = 0; i < _jointsCache.Length; i++)
            {
                if (i >= _dismemberDummies.Length)
                    break;
                if (_jointsCache[i].Joint.connectedBody == _dismemberDummies[i])
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
                var outward = jointTr.position - joint.connectedBody.position;
                if (outward.sqrMagnitude < 1e-8f)
                    outward = jointTr.up;
                else
                    outward.Normalize();

                RetargetToDummy(jointIndex, joint);

                results.Add(new DismemberedJoint
                {
                    WorldPosition = worldPos,
                    Outward = outward,
                });
            }

            return results.Count;
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

        private void RetargetToDummy(int jointIndex, CharacterJoint joint)
        {
            var dummy = _dismemberDummies[jointIndex];
            dummy.gameObject.SetActive(true);
            dummy.isKinematic = false;
            joint.connectedBody = dummy;
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
