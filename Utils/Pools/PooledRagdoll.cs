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
            public Transform Transform;
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
        public const string DismemberDummyName = "DismemberDummy";

        private bool _pendingReturnReset;
        private static readonly List<int> DisconnectCandidates = new(32);

        public static bool IsDismemberDummy(Component c) =>
            c != null && c.name == DismemberDummyName;

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
            RestoreJoints();
            EnqueueReturnReset(this);
        }

        /// <summary>
        /// Breaks a random number of joints in [DismemberMinJoints, DismemberMaxJointFraction * count]
        /// (or all if fewer than DismemberMinJoints).
        /// Distal joints stay connected so a severed arm can still dangle.
        /// Broken joints stay active, retargeted to a kinematic dummy on the distal part
        /// (null connectedBody would pin the piece to the world).
        /// </summary>
        public int DisconnectRandomJoints(List<DismemberedJoint> results)
        {
            results.Clear();
            if (_jointsCache == null || _jointsCache.Length == 0)
                return 0;

            DisconnectCandidates.Clear();
            for (var i = 0; i < _jointsCache.Length; i++)
            {
                var joint = _jointsCache[i].Joint;
                if (joint == null)
                    continue;
                var connected = joint.connectedBody != null
                    ? joint.connectedBody
                    : _jointsCache[i].ConnectedBody;
                if (connected == null || IsDismemberDummy(connected))
                    continue;
                if (_dismemberDummies == null ||
                    i >= _dismemberDummies.Length ||
                    _dismemberDummies[i] == null)
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
                var connected = joint.connectedBody != null ? joint.connectedBody : cache.ConnectedBody;
                if (joint == null || connected == null)
                    continue;

                var jointTr = joint.transform;
                var worldPos = jointTr.TransformPoint(joint.anchor);
                var outward = jointTr.position - connected.position;
                if (outward.sqrMagnitude < 1e-8f)
                    outward = jointTr.up;
                else
                    outward.Normalize();

                RetargetToDummy(jointIndex, joint);

                results.Add(new DismemberedJoint
                {
                    WorldPosition = worldPos,
                    Transform = jointTr,
                    Outward = outward,
                });
            }

            return results.Count;
        }

        private void RestoreJoints()
        {
            DeactivateDummies();
            RestoreJointConnections();
        }

        private void RestoreJointConnections()
        {
            if (_jointsCache == null)
                return;
            for (var i = 0; i < _jointsCache.Length; i++)
            {
                var joint = _jointsCache[i].Joint;
                if (joint == null)
                    continue;
                joint.autoConfigureConnectedAnchor = true;
                joint.connectedBody = null;
                joint.connectedBody = _jointsCache[i].ConnectedBody;
            }
        }

        private void RetargetToDummy(int jointIndex, CharacterJoint joint)
        {
            var dummy = _dismemberDummies[jointIndex];
            dummy.gameObject.SetActive(true);
            dummy.transform.SetParent(joint.transform, false);
            dummy.transform.localPosition = joint.anchor;
            dummy.transform.localRotation = Quaternion.identity;
            joint.autoConfigureConnectedAnchor = false;
            joint.connectedBody = dummy;
            joint.connectedAnchor = Vector3.zero;
        }

        private void DeactivateDummies()
        {
            if (_dismemberDummies == null)
                return;
            for (var i = 0; i < _dismemberDummies.Length; i++)
            {
                var rb = _dismemberDummies[i];
                if (rb != null)
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
