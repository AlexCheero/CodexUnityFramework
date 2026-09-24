#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CodexFramework.Utils.Pools.Editor
{
    public sealed class PoolAsyncWorkQueueTests
    {
        private readonly List<GameObject> _roots = new();
        private PoolAsyncWorkQueue _queue;
        [SetUp] public void SetUp() => _queue = new PoolAsyncWorkQueue();
        [TearDown] public void TearDown()
        {
            foreach (var root in _roots) if (root) Object.DestroyImmediate(root);
            _roots.Clear();
        }

        private ObjectPool CreatePool(string name, int count = 4)
        {
            var root = new GameObject(name);
            _roots.Add(root);
            var prototype = new GameObject(name + " item").AddComponent<PoolItem>();
            prototype.transform.SetParent(root.transform);
            var pool = root.AddComponent<ObjectPool>();
            pool.Init(prototype, count, count);
            pool.UseAsyncBudget(_queue);
            pool.AllocateAll();

            return pool;
        }

        [Test]
        public void SharedCountBudgetIsFairAcrossPoolsAndCannotBeSpentTwiceInOneFrame()
        {
            var a = CreatePool("a");
            var b = CreatePool("b");
            var order = new List<string>();
            for (var i = 0; i < 4; i++)
            {
                a.GetAsync(_ => order.Add("a"));
                b.GetAsync(_ => order.Add("b"));
            }
            Assert.IsEmpty(order);
            Assert.AreEqual(0, a.ActiveCount);
            _queue.Process(100, 3, 10000);
            CollectionAssert.AreEqual(new[] { "a", "b", "a" }, order);
            _queue.Process(100, 3, 10000);
            Assert.AreEqual(3, order.Count);
            _queue.Process(101, 3, 10000);
            CollectionAssert.AreEqual(new[] { "a", "b", "a", "b", "a", "b" }, order);
        }

        [Test]
        public void ReturnedItemsScheduleCallbacksWithoutRunningThemInsideReturn()
        {
            var pool = CreatePool("return", 1);
            PoolItem item = null;
            pool.GetAsync(value => item = value);
            _queue.Process(100, 1, 10000);
            var called = false;
            pool.GetAsync(_ => called = true);
            item.ReturnToPool();
            Assert.IsFalse(called);
            Assert.AreEqual(0, pool.ActiveCount);
            _queue.Process(100, 1, 10000);
            Assert.IsFalse(called);
            _queue.Process(101, 1, 10000);
            Assert.IsTrue(called);
        }

        [Test]
        public void CallbackCostClosesTheSoftTimeBudgetButOneItemAlwaysProgresses()
        {
            var pool = CreatePool("time");
            var calls = 0;
            for (var i = 0; i < 3; i++) pool.GetAsync(_ => { calls++; Thread.Sleep(3); });
            _queue.Process(100, 10, 0.1);
            Assert.AreEqual(1, calls);
            Assert.GreaterOrEqual(_queue.MillisecondsThisFrame, 0.1);
            _queue.Process(101, 10, 0.1);
            Assert.AreEqual(2, calls);
        }

        [Test]
        public void ReentrantPumpAndNewRequestsCannotBypassTheBudget()
        {
            var pool = CreatePool("nested");
            var calls = 0;
            pool.GetAsync(_ =>
            {
                calls++;
                pool.GetAsync(__ => calls++);
                _queue.Process(100, 1, 10000);
            });
            _queue.Process(100, 1, 10000);
            Assert.AreEqual(1, calls);
            _queue.Process(101, 1, 10000);
            Assert.AreEqual(2, calls);
        }

        [Test]
        public void CancellationBeforeAdmissionDoesNotActivateAnItem()
        {
            var pool = CreatePool("cancel");
            using var cancellation = new CancellationTokenSource();
            var called = false;
            pool.GetAsync(_ => called = true, cancellationToken: cancellation.Token);
            cancellation.Cancel();
            _queue.Process(100, 1, 10000);
            Assert.IsFalse(called);
            Assert.AreEqual(0, pool.ActiveCount);
            Assert.AreEqual(0, pool.PendingAsyncCount);
        }

#if CODEX_UNITASK_SUPPORT
        [Test]
        public void TaskAndPositionedOverloadsCannotBypassAdmission()
        {
            var pool = CreatePool("tasks");
            var position = new Vector3(7, 2, 3);
            var plain = pool.GetAsync();
            var positioned = pool.GetAsync(position);
            var rotated = pool.GetAsync(position, Quaternion.Euler(0, 45, 0));
            Assert.AreEqual(0, pool.ActiveCount);
            _queue.Process(100, 2, 10000);
            Assert.AreEqual(2, pool.ActiveCount);
            Assert.AreEqual(position, positioned.GetAwaiter().GetResult().transform.position);
            Assert.IsTrue(plain.GetAwaiter().IsCompleted);
            Assert.IsFalse(rotated.GetAwaiter().IsCompleted);
            _queue.Process(101, 2, 10000);
            Assert.AreEqual(position, rotated.GetAwaiter().GetResult().transform.position);
        }
#endif

        [Test]
        public void DestroyedPoolIsRemovedAndAnotherPoolStillCompletes()
        {
            var a = CreatePool("destroy");
            var b = CreatePool("survive");
            var failed = false;
            var completed = false;
            a.GetAsync(item => failed = item == null);
            b.GetAsync(item => completed = item != null);
            Object.DestroyImmediate(a.gameObject);
            _queue.Process(100, 1, 10000);
            Assert.IsTrue(failed);
            Assert.IsTrue(completed);
        }

        [Test]
        public void CanceledPoolDoesNotPreventAnotherPoolFromUsingThisFrame()
        {
            var a = CreatePool("canceled");
            var b = CreatePool("ready");
            using var cancellation = new CancellationTokenSource();
            a.GetAsync(_ => Assert.Fail("Canceled request was delivered"), cancellationToken: cancellation.Token);
            var ready = false;
            b.GetAsync(_ => ready = true);
            cancellation.Cancel();
            _queue.Process(100, 1, 10000);
            Assert.IsTrue(ready);
        }

        [Test]
        public void ForceGrowFalseKeepsDemandBoundedAndReportsExhaustionUnderTheBudget()
        {
            var pool = CreatePool("no growth", 1);
            typeof(ObjectPool).GetField("_maxCount", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).SetValue(pool, -1);
            pool.Get(false);
            var failed = false;
            pool.GetAsync(item => failed = item == null, forceGrow: false);
            Assert.IsFalse(failed);
            Assert.AreEqual(1, pool.Allocated);
            _queue.Process(100, 1, 10000);
            Assert.IsTrue(failed);
            Assert.AreEqual(1, pool.GrowTarget);
            Assert.AreEqual(1, pool.Allocated);
        }

        [Test]
        public void BudgetedPoolDoesNotDelayUnrelatedPoolManagerRequests()
        {
            var managerRoot = new GameObject("manager");
            _roots.Add(managerRoot);
            var manager = managerRoot.AddComponent<PoolManager>();
            var first = new GameObject("generic effect").AddComponent<PoolItem>();
            var second = new GameObject("generic projectile").AddComponent<PoolItem>();
            var a = manager.GetByPrototype(first, 2, 2);
            var b = manager.GetByPrototype(second, 2, 2);
            _roots.Add(a.gameObject);
            _roots.Add(b.gameObject);
            a.AllocateAll();
            b.AllocateAll();
            a.UseAsyncBudget(_queue);
            var budgeted = false;
            var unrelated = false;
            a.GetAsync(_ => budgeted = true);
            b.GetAsync(_ => unrelated = true);
            Assert.IsFalse(budgeted);
            Assert.IsTrue(unrelated);
            _queue.Process(100, 1, 10000);
            Assert.IsTrue(budgeted);
        }
    }
}
#endif
