using System.Collections.Generic;
using System.Diagnostics;
using Unity.Profiling;

namespace CodexFramework.Utils.Pools
{
    /// <summary>Shared, fair admission for expensive checkouts, including their reset and ready callbacks.</summary>
    public sealed class PoolAsyncWorkQueue
    {
        private static readonly ProfilerMarker ProcessMarker = new("PoolAsyncWorkQueue.Process");
        private readonly Queue<ObjectPool> _pools = new();
        private readonly HashSet<ObjectPool> _scheduled = new();
        private int _frame = -1;
        private bool _processing;
        public int CompletedThisFrame { get; private set; }
        public double MillisecondsThisFrame { get; private set; }
        public int PendingPools => _pools.Count;

        internal void Schedule(ObjectPool pool)
        {
            if (_scheduled.Add(pool)) _pools.Enqueue(pool);
        }

        public void Process(int frame, int maxCompletions, double maxMilliseconds)
        {
            if (_processing) return;
            using var marker = ProcessMarker.Auto();
            if (_frame != frame)
            {
                _frame = frame;
                CompletedThisFrame = 0;
                MillisecondsThisFrame = 0;
            }
            _processing = true;
            try
            {
                var idlePools = 0;
                while (_pools.Count > 0 && CompletedThisFrame < maxCompletions &&
                       (CompletedThisFrame == 0 || MillisecondsThisFrame < maxMilliseconds))
                {
                    var pool = _pools.Dequeue();
                    _scheduled.Remove(pool);
                    if (!pool)
                    {
                        pool.CancelPendingRequests();
                        continue;
                    }
                    var started = Stopwatch.GetTimestamp();
                    bool completed;
                    try { completed = pool.ProcessBudgetedWaiter(); }
                    finally
                    {
                        MillisecondsThisFrame += (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
                        if (pool && pool.PendingAsyncCount > 0) Schedule(pool);
                    }
                    if (completed)
                    {
                        CompletedThisFrame++;
                        idlePools = 0;
                    }
                    else if (pool.PendingAsyncCount > 0 && ++idlePools >= _pools.Count) break;
                }
            }
            finally { _processing = false; }
        }
    }
}
