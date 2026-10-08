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
        public int DisableObservationCount { get; private set; }
        public Vector3 PositionOnEnable { get; private set; }
        public Quaternion RotationOnEnable { get; private set; }
        public Vector3 PositionOnGet { get; private set; }
        public readonly List<string> Events = new();
        public int GetCount { get; private set; }
        public int ReturnCount { get; private set; }
        public bool ReturnDuringGet { get; set; }
        public bool ReturnDuringEnable { get; set; }
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
                PositionOnEnable = transform.position;
                RotationOnEnable = transform.rotation;
                Events.Add("enable");
            }
            if (ReturnDuringEnable && Item && !Item.IsInPool)
                Item.ReturnToPool();
        }

        private void OnDisable()
        {
            if (ObserveOnEnable) DisableObservationCount++;
            if (ReturnDuringDisable && Item && !Item.IsInPool)
                Item.ReturnToPool();
        }

        public void OnGet()
        {
            GetCount++;
            if (ObserveOnEnable)
            {
                PositionOnGet = transform.position;
                Events.Add("reset");
            }
            if (ReturnDuringGet)
                Item.ReturnToPool();
            if (ThrowDuringGet)
                throw new InvalidOperationException("intentional checkout reset failure");
        }

        public void OnReturn()
        {
            ReturnCount++;
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

        [UnityTest]
        public IEnumerator BatchPrewarm_UsesFrameBudgetWithoutCheckingOutOrReturningItems()
        {
            var pool = CreatePool(128, -1, 1,
                item => item.gameObject.AddComponent<ObjectPoolLifecycleProbe>());
            Assert.AreEqual(2, pool.Allocated);

            pool.PrewarmWithBatchGrowth();

            Assert.AreEqual(2, pool.Allocated,
                "Warmup must not spend the same frame's growth budget a second time.");
            Assert.AreEqual(32, pool.GrowTarget);
            Assert.AreEqual(0, pool.ActiveCount);
            Assert.AreEqual(0, pool.PendingAsyncCount);

            yield return WaitForAllocation(pool, 32, 1);

            Assert.AreEqual(32, pool.AvailableCount);
            Assert.AreEqual(0, pool.PendingAsyncCount);
            for (var i = 0; i < pool.Allocated; i++)
            {
                var item = pool.Items[i];
                var probe = item.GetComponent<ObjectPoolLifecycleProbe>();
                Assert.IsTrue(item.IsInPool, $"item {i}");
                Assert.IsFalse(item.gameObject.activeSelf, $"item {i}");
                Assert.AreEqual(0, probe.GetCount, $"item {i} checkout callbacks");
                Assert.AreEqual(0, probe.ReturnCount, $"item {i} return callbacks");
            }

            yield return null;
            yield return null;

            Assert.AreEqual(32, pool.Allocated,
                "Pending initial growth must not resume toward the prefab's old authored count.");
            Assert.AreEqual(32, pool.GrowTarget);
        }

        [UnityTest]
        public IEnumerator BatchPrewarm_UsesAuthoredCountForInitialAndThresholdGrowth()
        {
            var pool = CreatePool(128, -1, 2, item =>
            {
                var serialized = new SerializedObject(item);
                serialized.FindProperty("_prewarmCount").intValue = 8;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            });
            pool.PrewarmWithBatchGrowth();
            Assert.AreEqual(8, pool.GrowTarget);
            yield return WaitForAllocation(pool, 8, 2);

            for (var i = 0; i < 6; i++)
                Assert.NotNull(pool.Get());
            Assert.AreEqual(16, pool.GrowTarget);
            yield return WaitForAllocation(pool, 16, 2);
            Assert.AreEqual(6, pool.ActiveCount);
        }

        [UnityTest]
        public IEnumerator BatchGrowth_SchedulesThirtyTwoAtEachSeventyFivePercentBoundary()
        {
            var thresholds = new[] { 24, 48, 72 };
            var allocations = new[] { 32, 64, 96, 128 };
            foreach (var checkoutApi in new[] { "TryGet", "Get", "GetAsync" })
            {
                var pool = CreatePool(32, -1, 8);
                pool.PrewarmWithBatchGrowth();
                yield return WaitForAllocation(pool, 32, 8);
                for (var boundary = 0; boundary < thresholds.Length; boundary++)
                {
                    while (pool.ActiveCount < thresholds[boundary] - 1)
                        Assert.NotNull(CheckoutWithApi(pool, checkoutApi));
                    Assert.AreEqual(allocations[boundary], pool.GrowTarget,
                        $"{checkoutApi} scheduled growth before boundary {thresholds[boundary]}.");

                    Assert.NotNull(CheckoutWithApi(pool, checkoutApi));

                    Assert.AreEqual(thresholds[boundary], pool.ActiveCount);
                    Assert.AreEqual(allocations[boundary + 1], pool.GrowTarget,
                        $"{checkoutApi} must schedule the next batch on boundary {thresholds[boundary]}.");
                    Assert.That(pool.Allocated, Is.InRange(allocations[boundary], allocations[boundary] + 8),
                        "Threshold growth must respect the normal frame budget.");
                    Assert.AreEqual(0, pool.PendingAsyncCount);
                    yield return WaitForAllocation(pool, allocations[boundary + 1], 8);
                }
                Object.DestroyImmediate(_poolRoot);
                _poolRoot = null;
            }
        }

        [UnityTest]
        public IEnumerator BatchPrewarm_FulfillsAlreadyQueuedRequestsExactlyOnce()
        {
            var pool = CreatePool(2, -1, 1);
            Assert.NotNull(pool.Get());
            Assert.NotNull(pool.Get());
            var results = new PoolItem[3];
            var callbackCounts = new int[results.Length];
            for (var i = 0; i < results.Length; i++)
            {
                var requestIndex = i;
                pool.GetAsync(item =>
                {
                    results[requestIndex] = item;
                    callbackCounts[requestIndex]++;
                });
            }
            Assert.AreEqual(results.Length, pool.PendingAsyncCount);
            Assert.AreEqual(2, pool.Allocated,
                "The existing grow loop must retain its frame budget while these requests queue.");

            pool.PrewarmWithBatchGrowth();

            Assert.AreEqual(2, pool.Allocated);
            Assert.AreEqual(results.Length, pool.PendingAsyncCount);
            Assert.AreEqual(32, pool.GrowTarget);

            yield return WaitForAllocation(pool, 32, 1);

            Assert.AreEqual(2 + results.Length, pool.ActiveCount);
            Assert.AreEqual(0, pool.PendingAsyncCount);
            var distinctItems = new HashSet<PoolItem>();
            for (var i = 0; i < results.Length; i++)
            {
                Assert.AreEqual(1, callbackCounts[i], $"request {i}");
                Assert.NotNull(results[i], $"request {i}");
                Assert.IsTrue(distinctItems.Add(results[i]), $"request {i} repeated another lease");
            }
        }

        [UnityTest]
        public IEnumerator BatchPrewarm_InFlightLeasesAndReturnsPreserveCommittedBatches()
        {
            var pool = CreatePool(32, -1, 4);
            pool.PrewarmWithBatchGrowth();
            pool.PrewarmWithBatchGrowth();
            Assert.AreEqual(5, pool.Allocated);
            Assert.AreEqual(32, pool.GrowTarget);
            var leased = new List<PoolItem>();
            while (pool.TryGet(out var initialItem))
                leased.Add(initialItem);
            Assert.AreEqual(32, pool.GrowTarget,
                "Occupancy of a partially filled pool must be measured against its committed batch.");
            foreach (var item in leased)
                item.ReturnToPool();
            leased.Clear();

            yield return WaitForAllocation(pool, 32, 4);

            for (var i = 0; i < 24; i++)
                leased.Add(pool.Get());
            Assert.AreEqual(64, pool.GrowTarget);
            Assert.Less(pool.Allocated, 64);
            var availableWhileGrowing = pool.AvailableCount;
            for (var i = 0; i < availableWhileGrowing; i++)
            {
                Assert.IsTrue(pool.TryGet(out var item));
                leased.Add(item);
            }
            Assert.AreEqual(64, pool.GrowTarget,
                "Checkouts while the batch fills must not reserve a duplicate batch.");

#if CODEX_UNITASK_SUPPORT
            using var cancellation = new CancellationTokenSource();
            var canceledCallbackCount = 0;
            pool.GetAsync(_ => canceledCallbackCount++, cancellation.Token);
            Assert.AreEqual(1, pool.PendingAsyncCount);
            cancellation.Cancel();
#endif

            pool.PrewarmWithBatchGrowth();
            foreach (var item in leased)
                item.ReturnToPool();
            pool.PrewarmWithBatchGrowth();

            Assert.AreEqual(64, pool.GrowTarget,
                "Returning leases or canceling demand must not erase the promised refill batch.");
            Assert.AreEqual(0, pool.ActiveCount);

            yield return WaitForAllocation(pool, 64, 4);

#if CODEX_UNITASK_SUPPORT
            Assert.AreEqual(0, canceledCallbackCount);
            Assert.AreEqual(0, pool.PendingAsyncCount);
#endif
            Assert.AreEqual(64, pool.AvailableCount);
            for (var i = 0; i < 24; i++)
                Assert.NotNull(pool.Get());
            Assert.AreEqual(64, pool.Allocated,
                "Reusing returned items below the new threshold must not repeat the previous batch.");
            Assert.AreEqual(24, pool.ActiveCount);
        }

        [UnityTest]
        public IEnumerator BatchGrowth_DoesNotChangeOtherPoolsDemandBasedGrowth()
        {
            var warmedPool = CreatePool(32, -1, 8);
            var ordinaryPool = CreatePool(32, -1, 256);
            warmedPool.transform.SetParent(ordinaryPool.transform);
            warmedPool.PrewarmWithBatchGrowth();

            yield return WaitForAllocation(warmedPool, 32, 8);

            for (var i = 0; i < 24; i++)
            {
                Assert.NotNull(warmedPool.Get());
                Assert.NotNull(ordinaryPool.Get());
            }

            Assert.AreEqual(64, warmedPool.GrowTarget);
            Assert.Less(warmedPool.Allocated, 64);

            yield return WaitForAllocation(warmedPool, 64, 8);

            Assert.AreEqual(32, ordinaryPool.Allocated);
            Assert.AreEqual(32, ordinaryPool.GrowTarget);
            Assert.AreEqual(24, ordinaryPool.ActiveCount);
        }

        [UnityTest]
        public IEnumerator BatchGrowth_RespectsFixedPoolMaximum()
        {
            foreach (var maximum in new[] { 16, 40 })
            {
                var pool = CreatePool(1, maximum, 4);
                pool.PrewarmWithBatchGrowth();
                Assert.AreEqual(5, pool.Allocated);

                yield return WaitForAllocation(pool, Math.Min(32, maximum), 4);

                for (var i = 0; i < Math.Min(24, maximum); i++)
                    Assert.NotNull(pool.Get());
                Assert.AreEqual(maximum, pool.GrowTarget);

                yield return WaitForAllocation(pool, maximum, 4);

                for (var i = 0; i < maximum + 1; i++)
                    Assert.NotNull(pool.Get());
                PoolItem asyncResult = null;
                pool.GetAsync(item => asyncResult = item);

                Assert.NotNull(asyncResult);
                Assert.AreEqual(maximum, pool.Allocated);
                Assert.AreEqual(maximum, pool.ActiveCount);
                Assert.AreEqual(maximum, pool.Capacity);
                Assert.AreEqual(0, pool.PendingAsyncCount);
                Object.DestroyImmediate(_poolRoot);
                _poolRoot = null;
            }
        }

        [UnityTest]
        public IEnumerator BatchGrowth_FailedCheckoutDoesNotCrossActiveThreshold()
        {
            var pool = CreatePool(32, -1, 8,
                item => item.gameObject.AddComponent<ObjectPoolLifecycleProbe>());
            pool.PrewarmWithBatchGrowth();

            yield return WaitForAllocation(pool, 32, 8);

            for (var i = 0; i < 23; i++)
                Assert.NotNull(pool.Get());
            var failingProbe = pool.Items[23].GetComponent<ObjectPoolLifecycleProbe>();
            failingProbe.ThrowDuringGet = true;

            Assert.Throws<InvalidOperationException>(() => pool.Get());

            Assert.AreEqual(23, pool.ActiveCount);
            Assert.AreEqual(32, pool.Allocated);
            Assert.AreEqual(32, pool.GrowTarget);
            failingProbe.ThrowDuringGet = false;
            Assert.NotNull(pool.Get());
            Assert.AreEqual(24, pool.ActiveCount);
            Assert.AreEqual(64, pool.GrowTarget);
            Assert.Less(pool.Allocated, 64);

            yield return WaitForAllocation(pool, 64, 8);
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
        public void PositionedGet_DoesNotDisableTheItemDuringCheckout()
        {
            ObjectPoolLifecycleProbe probe = null;
            var pool = CreatePool(1, -1, 256,
                item => probe = item.gameObject.AddComponent<ObjectPoolLifecycleProbe>());
            probe.ReturnDuringDisable = true;
            probe.ObserveOnEnable = true;

            var item = pool.Get(Vector3.one);

            Assert.AreSame(probe.Item, item);
            Assert.AreEqual(1, pool.ActiveCount);
            Assert.AreEqual(1, probe.EnableObservationCount);
            Assert.AreEqual(0, probe.DisableObservationCount);
            Assert.IsFalse(item.IsInPool);
            Assert.IsTrue(item.gameObject.activeSelf);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OnEnableReturningItsOwnLease_IsRejectedWithoutReset(bool positioned)
        {
            ObjectPoolLifecycleProbe probe = null;
            var pool = CreatePool(1, -1, 256,
                item => probe = item.gameObject.AddComponent<ObjectPoolLifecycleProbe>());
            probe.ReturnDuringEnable = true;

            Assert.Throws<InvalidOperationException>(() =>
            {
                if (positioned) pool.Get(Vector3.one);
                else pool.Get();
            });

            Assert.AreEqual(0, probe.GetCount);
            Assert.AreEqual(0, pool.ActiveCount);
            Assert.AreEqual(1, pool.AvailableCount);
            Assert.IsTrue(probe.Item.IsInPool);
        }

        [TestCase("sync", false, false)]
        [TestCase("sync", true, false)]
        [TestCase("callback", false, false)]
        [TestCase("callback", true, false)]
        [TestCase("callback", false, true)]
        [TestCase("callback", true, true)]
#if CODEX_UNITASK_SUPPORT
        [TestCase("task", false, false)]
        [TestCase("task", true, false)]
        [TestCase("task", false, true)]
        [TestCase("task", true, true)]
#endif
        public void PositionedCheckout_AppliesPoseBeforeItsOnlyEnable(
            string api, bool setRotation, bool budgeted)
        {
            ObjectPoolLifecycleProbe probe = null;
            var pool = CreatePool(1, 1, 256,
                item => probe = item.gameObject.AddComponent<ObjectPoolLifecycleProbe>());
            var queue = new PoolAsyncWorkQueue();
            if (budgeted) pool.UseAsyncBudget(queue);
            var position = new Vector3(17f, -3f, 8f);
            var previousRotation = Quaternion.Euler(0f, 23f, 0f);
            var rotation = Quaternion.Euler(12f, 67f, -9f);
            probe.transform.rotation = previousRotation;
            probe.ObserveOnEnable = true;
            PoolItem result = null;
            if (api == "sync")
                result = setRotation ? pool.Get(position, rotation) : pool.Get(position);
            else if (api == "callback")
            {
                void Ready(PoolItem item)
                {
                    probe.Events.Add("ready");
                    result = item;
                }
                if (setRotation) pool.GetAsync(position, rotation, Ready);
                else pool.GetAsync(position, Ready);
            }
#if CODEX_UNITASK_SUPPORT
            else
            {
                var task = setRotation ? pool.GetAsync(position, rotation) : pool.GetAsync(position);
                if (budgeted)
                {
                    Assert.AreEqual(0, probe.EnableObservationCount);
                    queue.Process(100, 1, 10000);
                }
                result = task.GetAwaiter().GetResult();
            }
#endif
            if (budgeted && api == "callback")
            {
                Assert.IsNull(result);
                Assert.AreEqual(0, probe.EnableObservationCount);
                Assert.AreEqual(0, probe.GetCount);
                queue.Process(100, 1, 10000);
            }

            Assert.AreSame(probe.Item, result);
            Assert.AreEqual(1, probe.EnableObservationCount);
            Assert.AreEqual(0, probe.DisableObservationCount);
            Assert.AreEqual(1, probe.GetCount);
            Assert.IsFalse(probe.ObservedInPoolOnEnable);
            Assert.AreEqual(position, probe.PositionOnEnable);
            Assert.AreEqual(position, probe.PositionOnGet);
            Assert.Less(Quaternion.Angle(setRotation ? rotation : previousRotation, probe.RotationOnEnable), 0.01f);
            CollectionAssert.AreEqual(api == "callback"
                ? new[] { "enable", "reset", "ready" }
                : new[] { "enable", "reset" }, probe.Events);
        }

        [Test]
        public void QueuedPositionedRequest_PlacesReturnedItemBeforeReactivation()
        {
            ObjectPoolLifecycleProbe probe = null;
            var pool = CreatePool(1, -1, 1,
                item => probe = item.gameObject.AddComponent<ObjectPoolLifecycleProbe>());
            probe.ObserveOnEnable = true;
            var original = pool.Get();
            pool.GetAsync(_ => { });
            PoolItem result = null;
            var position = new Vector3(11f, 12f, 13f);
            var rotation = Quaternion.Euler(0f, 81f, 0f);
            pool.GetAsync(position, rotation, item => result = item);
            Assert.IsNull(result);
            Assert.AreEqual(1, pool.PendingAsyncCount);

            original.ReturnToPool();

            Assert.AreSame(original, result);
            Assert.AreEqual(position, probe.PositionOnEnable);
            Assert.Less(Quaternion.Angle(rotation, probe.RotationOnEnable), 0.01f);
            Assert.AreEqual(2, probe.EnableObservationCount);
            Assert.AreEqual(1, probe.DisableObservationCount,
                "Only the actual return should disable the item.");
            Assert.AreEqual(0, pool.PendingAsyncCount);
        }

        [Test]
        public void BudgetedPositionedRequests_KeepTheirOwnPoseAndCanceledRequestsStayInactive()
        {
            var pool = CreatePool(2, 2, 256,
                item => item.gameObject.AddComponent<ObjectPoolLifecycleProbe>());
            var queue = new PoolAsyncWorkQueue();
            pool.UseAsyncBudget(queue);
            foreach (var item in pool.Items)
                if (item) item.GetComponent<ObjectPoolLifecycleProbe>().ObserveOnEnable = true;
            using var cancellation = new CancellationTokenSource();
            var results = new List<PoolItem>();
            pool.GetAsync(Vector3.up, _ => Assert.Fail("Canceled request must not be delivered"), cancellation.Token);
            pool.GetAsync(new Vector3(3f, 4f, 5f), results, static (item, output) => output.Add(item));
            pool.GetAsync(new Vector3(7f, 8f, 9f), Quaternion.Euler(0f, 90f, 0f),
                results, static (item, output) => output.Add(item));
            cancellation.Cancel();
            queue.Process(100, 1, 10000);
            Assert.AreEqual(1, results.Count);
            queue.Process(101, 1, 10000);
            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(new Vector3(3f, 4f, 5f), results[0].transform.position);
            Assert.AreEqual(new Vector3(7f, 8f, 9f), results[1].transform.position);
            Assert.Less(Quaternion.Angle(Quaternion.Euler(0f, 90f, 0f), results[1].transform.rotation), 0.01f);
            foreach (var item in results)
            {
                var observed = item.GetComponent<ObjectPoolLifecycleProbe>();
                Assert.AreEqual(1, observed.EnableObservationCount);
                Assert.AreEqual(0, observed.DisableObservationCount);
            }
            Assert.AreEqual(0, pool.PendingAsyncCount);
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

        private static IEnumerator WaitForAllocation(ObjectPool pool, int target, int growPerFrame)
        {
            const int maximumFrames = 160;
            for (var frame = 0; pool.Allocated < target && frame < maximumFrames; frame++)
            {
                var allocatedBeforeFrame = pool.Allocated;
                yield return null;
                // EditMode iterator yields can span multiple allocator updates; PlayMode
                // resumes once per game frame and can verify the actual allocation budget.
                if (Application.isPlaying)
                    Assert.That(pool.Allocated - allocatedBeforeFrame, Is.InRange(0, growPerFrame),
                        "Async growth exceeded the configured allocation budget in one frame.");
            }
            Assert.AreEqual(target, pool.Allocated, "Async pool growth did not finish its promised batch.");
        }

        private static PoolItem CheckoutWithApi(ObjectPool pool, string checkoutApi)
        {
            switch (checkoutApi)
            {
                case "TryGet":
                    Assert.IsTrue(pool.TryGet(out var item));
                    return item;
                case "Get":
                    return pool.Get();
                case "GetAsync":
                    PoolItem result = null;
                    var callbackCount = 0;
                    pool.GetAsync(ready =>
                    {
                        result = ready;
                        callbackCount++;
                    });
                    Assert.AreEqual(1, callbackCount);
                    return result;
                default:
                    throw new ArgumentOutOfRangeException(nameof(checkoutApi), checkoutApi, null);
            }
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
