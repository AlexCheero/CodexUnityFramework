using System;
using UnityEngine;
#if CODEX_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;
#else
using System.Collections;
using CodexFramework.Helpers;
#endif

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

#if CODEX_UNITASK_SUPPORT
        private CancellationTokenSource _returnCts;
#else
        private Coroutine _returnRoutine;
#endif

        public void OnGet()
        {
            CancelReturnReset();

            for (var i = 0; i < _children.Length; i++)
                _children[i].Reapply();

            for (var i = 0; i < _jointsCache.Length; i++)
            {
                var joint = _jointsCache[i].Joint;
                joint.connectedBody = null;
                joint.connectedBody = _jointsCache[i].ConnectedBody;
            }

            for (var i = 0; i < _rigidbodies.Length; i++)
            {
                var rb = _rigidbodies[i];
                // Return reset may have been cancelled mid-way.
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = false;
            }
        }

        public void OnReturn()
        {
            // Item is already back in the pool (ReturnItem deactivates + frees the slot before OnReturn).
            CancelReturnReset();
#if CODEX_UNITASK_SUPPORT
            _returnCts = new CancellationTokenSource();
            ReturnResetAsync(_returnCts.Token).Forget();
#else
            // Own GO is inactive here — run on CoroutineRunner.
            _returnRoutine = CoroutineRunner.Instance.StartCoroutine(ReturnResetRoutine());
#endif
        }

        private void OnDestroy() => CancelReturnReset();

        private void CancelReturnReset()
        {
#if CODEX_UNITASK_SUPPORT
            if (_returnCts == null)
                return;

            _returnCts.Cancel();
            _returnCts.Dispose();
            _returnCts = null;
#else
            if (_returnRoutine == null)
                return;

            if (CoroutineRunner.Instance != null)
                CoroutineRunner.Instance.StopCoroutine(_returnRoutine);
            _returnRoutine = null;
#endif
        }

#if CODEX_UNITASK_SUPPORT
        private async UniTaskVoid ReturnResetAsync(CancellationToken ct)
        {
            try
            {
                for (var i = 0; i < _rigidbodies.Length; i++)
                {
                    if (ct.IsCancellationRequested)
                        return;

                    var rb = _rigidbodies[i];
                    rb.linearVelocity = Vector3.zero;

                    if (await UniTask.Yield(PlayerLoopTiming.Update, ct).SuppressCancellationThrow())
                        return;

                    rb.angularVelocity = Vector3.zero;
                    rb.isKinematic = true;

                    if (await UniTask.Yield(PlayerLoopTiming.Update, ct).SuppressCancellationThrow())
                        return;
                }
            }
            finally
            {
                if (_returnCts != null && _returnCts.Token == ct)
                {
                    _returnCts.Dispose();
                    _returnCts = null;
                }
            }
        }
#else
        private IEnumerator ReturnResetRoutine()
        {
            for (var i = 0; i < _rigidbodies.Length; i++)
            {
                var rb = _rigidbodies[i];
                rb.linearVelocity = Vector3.zero;
                yield return null;

                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                yield return null;
            }

            _returnRoutine = null;
        }
#endif
    }
}
