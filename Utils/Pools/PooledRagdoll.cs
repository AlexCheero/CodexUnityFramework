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

        private bool _corpseBakePhysicsSuspended;
        private readonly List<Rigidbody> _corpseBakeRigidbodies = new(16);
        private readonly List<Collider> _corpseBakeColliders = new(16);
        private bool[] _corpseBakeWasKinematic;
        private bool[] _corpseBakeDetectedCollisions;
        private bool[] _corpseBakeColliderEnabled;

        public bool IsPhysicsSuspendedForCorpseBake => _corpseBakePhysicsSuspended;

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
            RestorePhysicsAfterCorpseBakeSuspension();
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
                rb.WakeUp();
            }
        }

        public void OnReturn()
        {
            RestorePhysicsAfterCorpseBakeSuspension();
            ResetVisualState();
            RestoreBorrowedDummies();
            DeactivateDummies();
            RestoreJointConnections();
            EnqueueReturnReset(this);
        }

        /// <summary>
        /// Freezes the exact current ragdoll pose while it waits for a later-frame combined-mesh
        /// bake. Renderers and the pooled root stay active, so the corpse remains visible.
        /// </summary>
        public void SuspendPhysicsUntilCorpseBake()
        {
            if (_corpseBakePhysicsSuspended)
                return;

            EnsureCorpseBakePhysicsCache();
            for (var i = 0; i < _corpseBakeColliders.Count; i++)
            {
                var collider = _corpseBakeColliders[i];
                if (collider == null)
                    continue;
                _corpseBakeColliderEnabled[i] = collider.enabled;
            }

            for (var i = 0; i < _corpseBakeRigidbodies.Count; i++)
            {
                var rigidbody = _corpseBakeRigidbodies[i];
                if (rigidbody == null)
                    continue;

                _corpseBakeWasKinematic[i] = rigidbody.isKinematic;
                _corpseBakeDetectedCollisions[i] = rigidbody.detectCollisions;
            }

            _corpseBakePhysicsSuspended = true;
            for (var i = 0; i < _corpseBakeColliders.Count; i++)
            {
                var collider = _corpseBakeColliders[i];
                if (collider != null)
                    collider.enabled = false;
            }

            for (var i = 0; i < _corpseBakeRigidbodies.Count; i++)
            {
                var rigidbody = _corpseBakeRigidbodies[i];
                if (rigidbody == null)
                    continue;
                if (!rigidbody.isKinematic)
                {
                    rigidbody.linearVelocity = Vector3.zero;
                    rigidbody.angularVelocity = Vector3.zero;
                }

                rigidbody.detectCollisions = false;
                rigidbody.isKinematic = true;
            }

            // Unity's 3D Joint has no enabled switch. Once every endpoint (including
            // dismemberment dummies) is kinematic and collision-free, there is no dynamic
            // joint island to solve. Keep connectedBody untouched: null would pin to world
            // and would also lose the exact current dismemberment topology.
        }

        /// <summary>Idempotent pool safety reset, called on both return and checkout.</summary>
        private void RestorePhysicsAfterCorpseBakeSuspension()
        {
            if (!_corpseBakePhysicsSuspended)
                return;

            _corpseBakePhysicsSuspended = false;

            // Restore collider participation while every suspended body is still kinematic,
            // then restore each body's collision and kinematic state exactly as captured.
            for (var i = 0; i < _corpseBakeColliders.Count; i++)
            {
                var collider = _corpseBakeColliders[i];
                if (collider != null)
                    collider.enabled = _corpseBakeColliderEnabled[i];
            }

            for (var i = 0; i < _corpseBakeRigidbodies.Count; i++)
            {
                var rigidbody = _corpseBakeRigidbodies[i];
                if (rigidbody == null)
                    continue;
                rigidbody.detectCollisions = _corpseBakeDetectedCollisions[i];
                rigidbody.isKinematic = _corpseBakeWasKinematic[i];
            }

            ClearCorpseBakePhysicsCache();
        }

        private void EnsureCorpseBakePhysicsCache()
        {
            // Pool lives can add, remove or replace optional ragdoll pieces. Refresh the live
            // component set for each suspension, then retain it unchanged until restoration.
            // List capacity is reused, so the warmed path does not allocate.
            _corpseBakeRigidbodies.Clear();
            GetComponentsInChildren(true, _corpseBakeRigidbodies);
            _corpseBakeColliders.Clear();
            GetComponentsInChildren(true, _corpseBakeColliders);

            if (_corpseBakeWasKinematic == null ||
                _corpseBakeDetectedCollisions == null ||
                _corpseBakeWasKinematic.Length < _corpseBakeRigidbodies.Count ||
                _corpseBakeDetectedCollisions.Length < _corpseBakeRigidbodies.Count)
            {
                _corpseBakeWasKinematic = new bool[_corpseBakeRigidbodies.Count];
                _corpseBakeDetectedCollisions = new bool[_corpseBakeRigidbodies.Count];
            }

            if (_corpseBakeColliderEnabled == null ||
                _corpseBakeColliderEnabled.Length < _corpseBakeColliders.Count)
            {
                _corpseBakeColliderEnabled = new bool[_corpseBakeColliders.Count];
            }
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
            EnsureRuntimeDismemberDummies();
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

        /// <summary>
        /// Selects one independently severed, leaf-most gib for emergency manual motion.
        /// The deepest-cut rule prevents a moved transform from carrying another separately
        /// severed descendant with it. Fastest velocity wins, with cache order as a stable tie.
        /// </summary>
        public bool TryGetEmergencyDismemberedMotionBody(out Rigidbody movingBody)
        {
            movingBody = null;
            var bestSpeedSq = -1f;
            for (var i = 0; i < _jointsCache.Length; i++)
            {
                if (!TryGetPrimaryDismemberedBody(i, out var candidate))
                    continue;

                var containsDeeperCut = false;
                for (var j = 0; j < _jointsCache.Length; j++)
                {
                    if (j == i || !TryGetPrimaryDismemberedBody(j, out var other))
                        continue;
                    if (!other.transform.IsChildOf(candidate.transform))
                        continue;
                    containsDeeperCut = true;
                    break;
                }

                if (containsDeeperCut)
                    continue;

                var speedSq = candidate.linearVelocity.sqrMagnitude;
                if (speedSq <= bestSpeedSq)
                    continue;
                bestSpeedSq = speedSq;
                movingBody = candidate;
            }

            return movingBody != null;
        }

        private bool TryGetPrimaryDismemberedBody(int jointIndex, out Rigidbody body)
        {
            body = null;
            if (_dismemberDummies == null ||
                (uint)jointIndex >= (uint)_jointsCache.Length ||
                (uint)jointIndex >= (uint)_dismemberDummies.Length)
            {
                return false;
            }

            var joint = _jointsCache[jointIndex].Joint;
            if (joint == null || !joint.TryGetComponent(out body) || body == null)
                return false;

            // A borrowed high-detail body also has its own joint redirected to a dedicated
            // dummy, but that redirect only supports another primary cut; it is not a gib.
            if (IsBorrowedDummy(body))
                return false;

            var connected = joint.connectedBody;
            return connected == _dismemberDummies[jointIndex] || IsBorrowedDummy(connected);
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
            dummy.isKinematic = _corpseBakePhysicsSuspended;
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
                dedicated.isKinematic = _corpseBakePhysicsSuspended;
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

        /// <summary>
        /// Player-build safety for an older prefab whose serialized dummy references were
        /// never recached. Valid prefabs do no work here; malformed ones repair once, lazily
        /// on their first dismemberment, instead of dereferencing a null dummy.
        /// </summary>
        private void EnsureRuntimeDismemberDummies()
        {
            var jointCount = _jointsCache.Length;
            if (_dismemberDummies == null || _dismemberDummies.Length != jointCount)
                Array.Resize(ref _dismemberDummies, jointCount);

            var createdAny = false;
            for (var i = 0; i < jointCount; i++)
            {
                if (_dismemberDummies[i] != null)
                    continue;

                var joint = _jointsCache[i].Joint;
                if (joint == null)
                    continue;

                var go = new GameObject("DismemberDummy");
                go.SetActive(false);
                go.layer = joint.gameObject.layer;
                var dummyTransform = go.transform;
                dummyTransform.SetParent(joint.transform, false);
                dummyTransform.SetLocalPositionAndRotation(joint.anchor, Quaternion.identity);
                dummyTransform.localScale = Vector3.one;

                var dummy = go.AddComponent<Rigidbody>();
                dummy.isKinematic = true;
                dummy.useGravity = false;
                dummy.detectCollisions = false;
                dummy.interpolation = RigidbodyInterpolation.None;
                _dismemberDummies[i] = dummy;
                createdAny = true;
            }

            if (!createdAny)
                return;

            // A previous pool life may already have built this lazy suspension cache.
            // Do not discard an active snapshot before OnReturn has restored it. Newly
            // created dummies start inert; refresh the cache after restoration instead.
            if (_corpseBakePhysicsSuspended)
                return;

            ClearCorpseBakePhysicsCache();
        }

        private void ClearCorpseBakePhysicsCache()
        {
            _corpseBakeRigidbodies.Clear();
            _corpseBakeColliders.Clear();
        }

        private void RestoreBorrowedDummies()
        {
            for (var i = 0; i < _borrowedDismemberDummies.Count; i++)
                _borrowedDismemberDummies[i].useGravity = true;
            _borrowedDismemberDummies.Clear();
        }

        private void DeactivateDummies()
        {
            if (_dismemberDummies == null)
                return;

            for (var i = 0; i < _dismemberDummies.Length; i++)
            {
                var rb = _dismemberDummies[i];
                if (rb == null)
                    continue;
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
