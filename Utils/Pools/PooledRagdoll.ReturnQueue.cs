using System.Collections.Generic;
using UnityEngine;
#if CODEX_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Collections;
using CodexFramework.Helpers;
#endif

namespace CodexFramework.Utils.Pools
{
    public partial class PooledRagdoll
    {
        private static readonly Queue<PooledRagdoll> _returnQueue = new();
        private static int _returnBatchCount = 8;
        private static bool _isProcessing;

        public static void InitReturnBatchCount(int count) =>
            _returnBatchCount = count < 1 ? 1 : count;

        private static void EnqueueReturnReset(PooledRagdoll ragdoll)
        {
            if (ragdoll._pendingReturnReset)
                return;

            ragdoll._pendingReturnReset = true;
            _returnQueue.Enqueue(ragdoll);
            EnsureProcessing();
        }

        private static void EnsureProcessing()
        {
            if (_isProcessing)
                return;

            _isProcessing = true;
#if CODEX_UNITASK_SUPPORT
            ProcessReturnQueueAsync().Forget();
#else
            CoroutineRunner.Instance.StartCoroutine(ProcessReturnQueueRoutine());
#endif
        }

#if CODEX_UNITASK_SUPPORT
        private static async UniTaskVoid ProcessReturnQueueAsync()
        {
            try
            {
                while (_returnQueue.Count > 0)
                {
                    ProcessReturnBatch();
                    if (_returnQueue.Count > 0)
                        await UniTask.Yield(PlayerLoopTiming.Update);
                }
            }
            finally
            {
                _isProcessing = false;
                if (_returnQueue.Count > 0)
                    EnsureProcessing();
            }
        }
#else
        private static IEnumerator ProcessReturnQueueRoutine()
        {
            try
            {
                while (_returnQueue.Count > 0)
                {
                    ProcessReturnBatch();
                    if (_returnQueue.Count > 0)
                        yield return null;
                }
            }
            finally
            {
                _isProcessing = false;
                if (_returnQueue.Count > 0)
                    EnsureProcessing();
            }
        }
#endif

        private static void ProcessReturnBatch()
        {
            var processed = 0;
            while (_returnQueue.Count > 0 && processed < _returnBatchCount)
            {
                var ragdoll = _returnQueue.Dequeue();
                if (ragdoll == null)
                    continue;

                ragdoll._pendingReturnReset = false;

                // Reclaimed from pool before reset — drop without spending batch budget.
                if (ragdoll.gameObject.activeSelf)
                    continue;

                ragdoll.ResetRigidbodies();
                processed++;
            }
        }
    }
}
