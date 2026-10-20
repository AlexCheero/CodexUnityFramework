using System.Collections.Generic;
using System.Diagnostics;
using Unity.Profiling;

namespace CodexFramework.Utils.Pools
{
    /// <summary>One soft budget for construction and async checkout, including reset and ready callbacks.</summary>
    public sealed class PoolAsyncWorkQueue
    {
        private static readonly ProfilerMarker ProcessMarker = new("PoolAsyncWorkQueue.Process");
        private readonly List<ObjectPool> _pools = new(64);
        private readonly List<ObjectPool> _deferred = new(64);
        private readonly HashSet<ObjectPool> _scheduled = new();
        private int _frame = -1;
        private bool _processing;
        public int CompletedThisFrame { get; private set; }
        public long TotalCompleted { get; private set; }
        public double MillisecondsThisFrame { get; private set; }
        public int PendingPools => _pools.Count + _deferred.Count;

        internal void Schedule(ObjectPool pool)
        {
            if (_scheduled.Add(pool)) _pools.Add(pool);
        }

        public void Process(int frame, int maxOperations, double maxMilliseconds)
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
                while (_pools.Count > 0 && CompletedThisFrame < maxOperations &&
                       (CompletedThisFrame == 0 || MillisecondsThisFrame < maxMilliseconds))
                {
                    // First among equal priorities wins; completed work goes to the tail.
                    var selected = 0;
                    for (var i = 1; i < _pools.Count; i++)
                        if (!_pools[i] || _pools[selected] && _pools[i].Priority > _pools[selected].Priority)
                            selected = i;
                    var pool = _pools[selected];
                    _pools.RemoveAt(selected);
                    if (!pool)
                    {
                        _scheduled.Remove(pool);
                        pool.CancelPendingRequests();
                        continue;
                    }
                    var started = Stopwatch.GetTimestamp();
                    var completed = false;
                    try { completed = pool.ProcessBudgetedWork(frame); }
                    finally
                    {
                        MillisecondsThisFrame += (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
                        if (pool && pool.HasBudgetedWork && !completed)
                        {
                            // At its own growth limit, this pool must let other priorities run.
                            // Keep its scheduled bit while deferred to prevent reentrant duplicates.
                            _deferred.Add(pool);
                        }
                        else
                        {
                            _scheduled.Remove(pool);
                            if (pool && pool.HasBudgetedWork) Schedule(pool);
                        }
                    }
                    if (completed)
                    {
                        CompletedThisFrame++;
                        TotalCompleted++;
                    }
                }
            }
            finally
            {
                _pools.AddRange(_deferred);
                _deferred.Clear();
                _processing = false;
            }
        }
    }
}
