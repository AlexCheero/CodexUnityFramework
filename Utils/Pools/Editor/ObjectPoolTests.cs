#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
#if CODEX_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#endif
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CodexFramework.Utils.Pools.Editor
{
    [ExecuteAlways]
    internal sealed class ObjectPoolLifecycleProbe : MonoBehaviour,
        IResetOnGetPoolableBehaviour,
        IResetOnReturnPoolableBehaviour
    {
        public PoolItem Item { get; private set; }
        public bool ObserveOnEnable { get; set; }
        public bool ObservedInPoolOnEnable { get; private set; }
        public int EnableObservationCount { get; private set; }
        public bool ReturnDuringGet { get; set; }
        public bool ThrowDuringGet { get; set; }
        public bool TryGetDuringReturn { get; set; }
        public bool ReturnDuringDisable { get; set; }
        public PoolItem DestroyDuringReturn { get; set; }
        public PoolItem GetDuringReturnResult { get; private set; }

        private void Awake() => Item = GetComponent<PoolItem>();

        private void OnEnable()
        {
            if (ObserveOnEnable)
            {
                ObservedInPoolOnEnable = Item.IsInPool;
                EnableObservationCount++;
            }
        }

        private void OnDisable()
        {
            if (ReturnDuringDisable && Item && !Item.IsInPool)
                Item.ReturnToPool();
        }

        public void OnGet()
        {
            if (ReturnDuringGet)
                Item.ReturnToPool();
            if (ThrowDuringGet)
                throw new InvalidOperationException("intentional checkout reset failure");
        }

        public void OnReturn()
        {
            if (TryGetDuringReturn)
                GetDuringReturnResult = Item.Pool.Get(false);
            if (DestroyDuringReturn)
                Object.DestroyImmediate(DestroyDuringReturn.gameObject);
        }
    }

    public sealed class ObjectPoolTests
    {
        private GameObject _poolRoot;

        [TearDown]
        public void TearDown()
        {
            if (_poolRoot)
                Object.DestroyImmediate(_poolRoot);
        }

        [Test]
        public void ResizeArray_DoesNotGrowWhenRequestedIndexAlreadyFits()
        {
            var values = new int[128];
            var original = values;

            CodexECS.Utility.Utils.ResizeArray(10, ref values, 64);

            Assert.AreSame(original, values);
            Assert.AreEqual(128, values.Length);
        }

        [Test]
        public void ResizeArray_NegativeLastIndexDoesNotAllocateOrGrow()
        {
            int[] empty = null;
            var values = new int[8];
            var original = values;

            CodexECS.Utility.Utils.ResizeArray(-1, ref empty, 64);
            CodexECS.Utility.Utils.ResizeArray(-1, ref values, 64);

            Assert.IsNull(empty);
            Assert.AreSame(original, values);
        }

        [Test]
        public void Init_DoesNotResizeCapacityWhenInitialDemandAlreadyFits()
        {
            var pool = CreatePool(128, -1, 1);

            Assert.AreEqual(128, pool.Capacity);
            Assert.AreEqual(128, pool.GrowTarget);
            Assert.AreEqual(2, pool.Allocated,
                "The adopted prototype plus this frame's one-item grow budget should be instantiated.");
        }

        [Test]
        public void NonPowerOfTwoInitialCount_CreatesOnlyConfiguredDemand()
        {
            var pool = CreatePool(100, -1, 256);

            Assert.AreEqual(100, pool.Allocated);
            Assert.AreEqual(128, pool.Capacity);
            Assert.AreEqual(100, pool.GrowTarget);
        }

        [Test]
        public void InvalidGrowBudget_IsClampedInsteadOfStallingGrowth()
        {
            LogAssert.Expect(LogType.Error, "should add at least one object per frame");

            var pool = CreatePool(2, -1, 0);

            Assert.AreEqual(2, pool.Allocated);
            Assert.AreEqual(2, pool.GrowTarget);
        }

        [Test]
        public void BurstOfTwoThousandRequests_HasBoundedCapacityAndInstantiation()
        {
            var pool = CreatePool(1, -1, 1);
            Assert.NotNull(pool.Get());
            var completedCount = 0;

            for (var i = 0; i < 2_000; i++)
                pool.GetAsync(item =>
                {
                    if (item)
                        completedCount++;
                });

            Assert.AreEqual(2, pool.Allocated);
            Assert.AreEqual(2, pool.ActiveCount);
            Assert.AreEqual(1, completedCount);
            Assert.AreEqual(1_999, pool.PendingAsyncCount);
            Assert.AreEqual(2_001, pool.GrowTarget);
            Assert.That(pool.Capacity, Is.GreaterThanOrEqualTo(pool.GrowTarget));
            Assert.That(pool.Capacity, Is.LessThanOrEqualTo(2_048));
        }

        [UnityTest]
        public IEnumerator Growth_StopsAtDemandInsteadOfFillingSpareCapacity()
        {
            var pool = CreatePool(128, -1, 256);
            var leased = new List<PoolItem>(128);
            for (var i = 0; i < 128; i++)
                leased.Add(pool.Get());
            PoolItem extra = null;

            pool.GetAsync(item => extra = item);
            Assert.IsNull(extra);
            Assert.AreEqual(128, pool.Allocated);
            Assert.Greater(pool.Capacity, pool.Allocated);

            yield return null;
            yield return null;

            Assert.NotNull(extra);
            Assert.AreEqual(129, pool.Allocated);
            Assert.AreEqual(129, pool.ActiveCount);
            Assert.AreEqual(0, pool.PendingAsyncCount);
            Assert.AreEqual(129, pool.GrowTarget);
            Assert.Greater(pool.Capacity, pool.Allocated,
                "Reserved capacity must remain null spare storage instead of becoming clone demand.");
        }

        [Test]
        public void Return_FulfillsOldestWaiterWithoutInstantiating()
        {
            var pool = CreatePool(1, -1, 1);
            var prototype = pool.Get();
            PoolItem first = null;
            PoolItem second = null;
            PoolItem third = null;

            pool.GetAsync(item => first = item);
            pool.GetAsync(item => second = item);
            pool.GetAsync(item => third = item);
            Assert.NotNull(first);
            Assert.IsNull(second);
            Assert.IsNull(third);
            Assert.AreEqual(2, pool.Allocated);

            prototype.ReturnToPool();

            Assert.AreSame(prototype, second);
            Assert.IsNull(third);
            Assert.AreEqual(2, pool.Allocated);
            Assert.AreEqual(2, pool.ActiveCount);
            Assert.AreEqual(1, pool.PendingAsyncCount);
        }

        [Test]
        public void Return_PreservesActiveAndAvailableLeaseStateAcrossSwap()
        {
            var pool = CreatePool(2, 2, 256);
            var first = pool.Get();
            var second = pool.Get();

            first.ReturnToPool();

            Assert.IsTrue(first.IsInPool);
            Assert.IsFalse(second.IsInPool);
            Assert.AreEqual(1, pool.ActiveCount);
            Assert.AreEqual(1, pool.AvailableCount);

            second.ReturnToPool();

            Assert.IsTrue(first.IsInPool);
            Assert.IsTrue(second.IsInPool);
            Assert.AreEqual(0, pool.ActiveCount);
            Assert.AreEqual(2, pool.AvailableCount);
        }

        [Test]
        public void CheckoutAndReturnHooks_CannotObserveOrLeaseAnItemMidTransition()
        {
            ObjectPoolLifecycleProbe probe = null;
            var pool = CreatePool(1, -1, 256,
                item => probe = item.gameObject.AddComponent<ObjectPoolLifecycleProbe>());
            probe.ObserveOnEnable = true;

            var item = pool.Get();

            Assert.AreEqual(1, probe.EnableObservationCount);
            Assert.IsFalse(probe.ObservedInPoolOnEnable,
                "OnEnable must observe an already checked-out lease.");
            probe.TryGetDuringReturn = true;
            item.ReturnToPool();

            Assert.IsNull(probe.GetDuringReturnResult,
                "A returning item must not become available until every return hook finishes.");
            Assert.AreEqual(0, pool.ActiveCount);
            Assert.AreEqual(1, pool.AvailableCount);
        }

        [Test]
        public void OnGetReturningItsOwnLease_IsRejectedWithoutCorruptingPool()
        {
            ObjectPoolLifecycleProbe probe = null;
            var pool = CreatePool(1, -1, 256,
                item => probe = item.gameObject.AddComponent<ObjectPoolLifecycleProbe>());
            probe.ReturnDuringGet = true;

            Assert.Throws<InvalidOperationException>(() => pool.Get());

            Assert.AreEqual(0, pool.ActiveCount);
            Assert.AreEqual(1, pool.AvailableCount);
            Assert.IsTrue(probe.Item.IsInPool);
        }

        [Test]
        public void OnGetException_RollsLeaseBackIntoAvailablePartition()
        {
            ObjectPoolLifecycleProbe probe = null;
            var pool = CreatePool(1, -1, 256,
                item => probe = item.gameObject.AddComponent<ObjectPoolLifecycleProbe>());
            probe.ThrowDuringGet = true;

            Assert.Throws<InvalidOperationException>(() => pool.Get());

            Assert.AreEqual(0, pool.ActiveCount);
            Assert.AreEqual(1, pool.AvailableCount);
            Assert.IsTrue(probe.Item.IsInPool);
        }

        [Test]
        public void PositionedGet_OnDisableReturningLeaseIsRejectedWithoutReactivation()
        {
            ObjectPoolLifecycleProbe probe = null;
            var pool = CreatePool(1, -1, 256,
                item => probe = item.gameObject.AddComponent<ObjectPoolLifecycleProbe>());
            probe.ReturnDuringDisable = true;

            Assert.Throws<InvalidOperationException>(() => pool.Get(Vector3.one));

            Assert.AreEqual(0, pool.ActiveCount);
            Assert.AreEqual(1, pool.AvailableCount);
            Assert.IsTrue(probe.Item.IsInPool);
            Assert.IsFalse(probe.Item.gameObject.activeSelf);
        }

        [Test]
        public void ReturnHookDestroyingAnotherActiveItem_RepairsBeforePublishingReturn()
        {
            ObjectPoolLifecycleProbe probe = null;
            var pool = CreatePool(3, -1, 256,
                item => probe = item.gameObject.AddComponent<ObjectPoolLifecycleProbe>());
            var returning = pool.Get();
            var surviving = pool.Get();
            var destroyed = pool.Get();
            probe.DestroyDuringReturn = destroyed;

            returning.ReturnToPool();

            Assert.AreEqual(2, pool.Allocated);
            Assert.AreEqual(1, pool.ActiveCount);
            Assert.AreEqual(1, pool.AvailableCount);
            Assert.AreEqual(0, surviving.Idx);
            Assert.IsFalse(surviving.IsInPool);
            Assert.AreEqual(1, returning.Idx);
            Assert.IsTrue(returning.IsInPool);
            surviving.ReturnToPool();
            Assert.AreEqual(0, pool.ActiveCount);
            Assert.AreEqual(2, pool.AvailableCount);
        }

        [Test]
        public void FixedPool_NeverExceedsNonPowerOfTwoMaximumAndReclaims()
        {
            var pool = CreatePool(1, 100, 256);
            var leased = new HashSet<PoolItem>();
            for (var i = 0; i < 100; i++)
                leased.Add(pool.Get());
            PoolItem overflow = null;

            pool.GetAsync(item => overflow = item);

            Assert.AreEqual(100, leased.Count);
            Assert.NotNull(overflow);
            Assert.IsTrue(leased.Contains(overflow));
            Assert.AreEqual(100, pool.Capacity);
            Assert.AreEqual(100, pool.Allocated);
            Assert.AreEqual(100, pool.ActiveCount);
        }

        [Test]
        public void FixedPool_AllocatesTowardMaximumBeforeReclaimingActiveLeases()
        {
            var pool = CreatePool(1, 4, 1);
            var leased = new HashSet<PoolItem>();

            for (var i = 0; i < 4; i++)
                leased.Add(pool.Get());

            Assert.AreEqual(4, leased.Count);
            Assert.AreEqual(4, pool.Allocated);
            Assert.AreEqual(4, pool.ActiveCount);
            Assert.AreEqual(4, pool.Capacity);
        }

        [UnityTest]
        public IEnumerator FixedPool_QueuedDemandCompletesAtMaximumByReclaiming()
        {
            var pool = CreatePool(1, 4, 1);
            Assert.NotNull(pool.Get());
            Assert.NotNull(pool.Get());
            var results = new PoolItem[3];

            for (var i = 0; i < results.Length; i++)
            {
                var requestIndex = i;
                pool.GetAsync(item => results[requestIndex] = item);
            }

            Assert.AreEqual(3, pool.PendingAsyncCount);
            yield return null;
            yield return null;
            yield return null;

            Assert.That(results, Has.None.Null);
            Assert.AreSame(results[1], results[2],
                "Demand beyond the fixed maximum retains the pool's active-item reclaim contract.");
            Assert.AreEqual(0, pool.PendingAsyncCount);
            Assert.AreEqual(4, pool.Allocated);
            Assert.AreEqual(4, pool.ActiveCount);
        }

        [Test]
        public void DestroyedActiveSlot_IsCompactedAndCanBeReplaced()
        {
            var pool = CreatePool(2, -1, 256);
            var surviving = pool.Get();
            var destroyed = pool.Get();

            Object.DestroyImmediate(destroyed.gameObject);
            Assert.IsFalse(pool.TryGet(out _));

            Assert.AreEqual(1, pool.Allocated);
            Assert.AreEqual(1, pool.ActiveCount);
            Assert.AreEqual(0, pool.AvailableCount);
            Assert.AreEqual(0, surviving.Idx);
            Assert.IsFalse(surviving.IsInPool);
            surviving.ReturnToPool();
            Assert.AreEqual(0, pool.ActiveCount);
            Assert.AreSame(surviving, pool.Get());
            var replacement = pool.Get();
            Assert.NotNull(replacement);
            Assert.AreNotSame(surviving, replacement);
            Assert.AreEqual(2, pool.Allocated);
        }

        [Test]
        public void DestroyedAvailableSlot_IsCompactedAndCanBeReplaced()
        {
            var pool = CreatePool(2, -1, 256);
            var destroyed = pool.Items[1];

            Object.DestroyImmediate(destroyed.gameObject);
            var surviving = pool.Get();

            Assert.NotNull(surviving);
            Assert.AreEqual(1, pool.Allocated);
            Assert.AreEqual(1, pool.ActiveCount);
            Assert.AreEqual(0, surviving.Idx);
            Assert.IsFalse(surviving.IsInPool);
            var replacement = pool.Get();
            Assert.NotNull(replacement);
            Assert.AreNotSame(surviving, replacement);
            Assert.AreEqual(2, pool.Allocated);
            Assert.AreEqual(2, pool.ActiveCount);
        }

        [Test]
        public void ForceGrowFalse_CompletesImmediatelyWithNullWithoutChangingDemand()
        {
            var pool = CreatePool(1, -1, 1);
            Assert.NotNull(pool.Get());
            var callbackCount = 0;
            PoolItem result = null;
            var capacity = pool.Capacity;
            var allocated = pool.Allocated;
            var target = pool.GrowTarget;

            pool.GetAsync(item =>
            {
                callbackCount++;
                result = item;
            }, false);

            Assert.AreEqual(1, callbackCount);
            Assert.IsNull(result);
            Assert.AreEqual(capacity, pool.Capacity);
            Assert.AreEqual(allocated, pool.Allocated);
            Assert.AreEqual(target, pool.GrowTarget);
            Assert.AreEqual(0, pool.PendingAsyncCount);
        }

#if CODEX_UNITASK_SUPPORT
        [Test]
        public void AlreadyCanceledRequest_DoesNotCreateDemand()
        {
            var pool = CreatePool(1, -1, 1);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var capacity = pool.Capacity;
            var allocated = pool.Allocated;
            var target = pool.GrowTarget;

            var request = pool.GetAsync(cancellation.Token);

            Assert.AreEqual(UniTaskStatus.Canceled, request.Status);
            Assert.Throws<OperationCanceledException>(() => request.GetAwaiter().GetResult());
            Assert.AreEqual(capacity, pool.Capacity);
            Assert.AreEqual(allocated, pool.Allocated);
            Assert.AreEqual(target, pool.GrowTarget);
            Assert.AreEqual(0, pool.PendingAsyncCount);
        }

        [UnityTest]
        public IEnumerator Cancellation_RemovesQueuedDemandAndStopsGrowth()
        {
            var pool = CreatePool(1, -1, 1);
            Assert.NotNull(pool.Get());
            PoolItem firstAsync = null;
            pool.GetAsync(item => firstAsync = item);
            Assert.NotNull(firstAsync);
            using var cancellation = new CancellationTokenSource();
            var request = pool.GetAsync(cancellation.Token);
            Assert.AreEqual(1, pool.PendingAsyncCount);
            Assert.AreEqual(3, pool.GrowTarget);

            cancellation.Cancel();
            yield return null;
            yield return null;

            Assert.AreEqual(UniTaskStatus.Canceled, request.Status);
            Assert.Throws<OperationCanceledException>(() => request.GetAwaiter().GetResult());
            Assert.AreEqual(0, pool.PendingAsyncCount);
            Assert.AreEqual(2, pool.Allocated);
            Assert.AreEqual(2, pool.GrowTarget);
        }

        [UnityTest]
        public IEnumerator CancellationContinuation_CanReturnItemWithoutCorruptingWaiterQueue()
        {
            var pool = CreatePool(1, -1, 1);
            var prototype = pool.Get();
            PoolItem firstAsync = null;
            pool.GetAsync(item => firstAsync = item);
            Assert.NotNull(firstAsync);
            using var cancellation = new CancellationTokenSource();
            var canceledRequest = pool.GetAsync(cancellation.Token);
            PoolItem survivingResult = null;
            pool.GetAsync(item => survivingResult = item);
            var cancellationAwaiter = canceledRequest.GetAwaiter();
            cancellationAwaiter.OnCompleted(() =>
            {
                try
                {
                    _ = cancellationAwaiter.GetResult();
                }
                catch (OperationCanceledException)
                {
                }
                prototype.ReturnToPool();
            });

            cancellation.Cancel();
            yield return null;
            yield return null;

            Assert.AreEqual(UniTaskStatus.Canceled, canceledRequest.Status);
            Assert.AreSame(prototype, survivingResult);
            Assert.AreEqual(0, pool.PendingAsyncCount);
            Assert.AreEqual(2, pool.Allocated);
            Assert.AreEqual(2, pool.ActiveCount);
        }

        [UnityTest]
        public IEnumerator CallbackCancellation_SuppressesReadyCallbackAndRemovesDemand()
        {
            var pool = CreatePool(1, -1, 1);
            Assert.NotNull(pool.Get());
            pool.GetAsync(_ => { });
            using var cancellation = new CancellationTokenSource();
            var callbackCount = 0;
            pool.GetAsync(_ => callbackCount++, cancellation.Token);

            cancellation.Cancel();
            yield return null;
            yield return null;

            Assert.AreEqual(0, callbackCount);
            Assert.AreEqual(0, pool.PendingAsyncCount);
            Assert.AreEqual(2, pool.GrowTarget);
        }
#endif

        [UnityTest]
        public IEnumerator Destroy_CompletesEveryPendingWaiterExactlyOnceWithNull()
        {
            var pool = CreatePool(1, -1, 1);
            Assert.NotNull(pool.Get());
            var invocationCounts = new int[17];
            var receivedNull = new bool[17];

            for (var i = 0; i < invocationCounts.Length; i++)
            {
                var requestIndex = i;
                pool.GetAsync(item =>
                {
                    invocationCounts[requestIndex]++;
                    receivedNull[requestIndex] = !item;
                });
            }

            Assert.AreEqual(1, invocationCounts[0]);
            Assert.IsFalse(receivedNull[0]);
            Object.DestroyImmediate(_poolRoot);
            _poolRoot = null;

            for (var i = 0; i < invocationCounts.Length; i++)
            {
                Assert.AreEqual(1, invocationCounts[i], $"request {i}");
                Assert.AreEqual(i != 0, receivedNull[i], $"request {i}");
            }

            yield return null;

            for (var i = 0; i < invocationCounts.Length; i++)
                Assert.AreEqual(1, invocationCounts[i], $"late callback for request {i}");
        }

        private ObjectPool CreatePool(
            int initialCount,
            int maxCount,
            int growPerFrame,
            Action<PoolItem> configurePrototype = null)
        {
            _poolRoot = new GameObject("ObjectPoolTests");
            var prototype = new GameObject("Prototype").AddComponent<PoolItem>();
            configurePrototype?.Invoke(prototype);
            var serializedPrototype = new SerializedObject(prototype);
            serializedPrototype.FindProperty("_growPerFrame").intValue = growPerFrame;
            serializedPrototype.ApplyModifiedPropertiesWithoutUndo();
            var pool = _poolRoot.AddComponent<ObjectPool>();
            pool.Init(prototype, initialCount, maxCount);
            return pool;
        }
    }
}
#endif
