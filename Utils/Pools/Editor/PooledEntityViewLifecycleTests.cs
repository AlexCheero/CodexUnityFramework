#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using CodexECS;
using CodexFramework.CodexEcsUnityIntegration.Views;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CodexFramework.Utils.Pools.Editor
{
    public sealed class PooledEntityViewLifecycleTests
    {
        private struct Anchor : IComponent { }

        private GameObject _poolRoot;
        private ObjectPool _pool;
        private EcsWorld _world;
        private EntityView _view;

        [SetUp]
        public void SetUp()
        {
            _world = new EcsWorld();
            _poolRoot = new GameObject(nameof(PooledEntityViewLifecycleTests));
            var prototype = new GameObject("Prototype").AddComponent<PoolItem>();
            _view = prototype.gameObject.AddComponent<EntityView>();
            SetField(_view, "_components", new List<ComponentWrapper> { new ComponentWrapper<Anchor>() });
            SetField(_view, "_unityComponentsBuffer", new List<Component> { prototype.transform });
            var pooledView = prototype.gameObject.AddComponent<PooledEntityView>();
            SetField(pooledView, "_view", _view);
            _pool = _poolRoot.AddComponent<ObjectPool>();
            _pool.Init(prototype, 1, 1);
        }

        [TearDown]
        public void TearDown()
        {
            if (_poolRoot)
                Object.DestroyImmediate(_poolRoot);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CancelFreshRagdollCheckoutBeforeInitialization_ReturnsWithoutAnEntity(bool livingWasDeleted)
        {
            Exception returnError = null;
            var callbacks = 0;
            var livingId = _world.Create();
            _world.Add<Anchor>(livingId);
            var payloadType = typeof(RagdollFallHelper).GetNestedType("KnockbackRagdollSpawnPayload", BindingFlags.NonPublic);
            var payload = Activator.CreateInstance(payloadType);
            payloadType.GetField("World").SetValue(payload, _world);
            payloadType.GetField("LivingEntity").SetValue(payload, _world.GetById(livingId));
            if (livingWasDeleted)
                _world.Delete(livingId);
            var spawnCallback = typeof(RagdollFallHelper).GetMethod("OnKnockbackRagdollSpawned", BindingFlags.Static | BindingFlags.NonPublic);

            _pool.GetAsync(Vector3.one, Quaternion.identity, payload, (item, state) =>
            {
                callbacks++;
                // The same async callback returns the fresh lease when the living
                // mob is gone or no longer knocked down, before InitAsEntity runs.
                try { spawnCallback.Invoke(null, new[] { item, state }); }
                catch (Exception error) { returnError = error; }
            });

            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(returnError, Is.Null);
            Assert.That(_pool.ActiveCount, Is.Zero);
            Assert.That(_pool.AvailableCount, Is.EqualTo(1));
            AssertUnbound();
            Assert.DoesNotThrow(_view.DeleteFromWorld);
        }

        [Test]
        public void InitializedCheckout_ReturnsAndReinitializesWithoutDeletingTheNextEntity()
        {
            var firstItem = _pool.Get();
            _view.InitAsEntity(_world);
            var firstEntity = _view.Entity;

            firstItem.ReturnToPool();

            Assert.That(_world.IsEntityValid(firstEntity), Is.False);
            AssertUnbound();
            Assert.DoesNotThrow(_view.DeleteFromWorld);
            var nextItem = _pool.Get();
            Assert.That(nextItem, Is.SameAs(firstItem));
            _view.InitAsEntity(_world);
            var nextEntity = _view.Entity;
            Assert.That(nextEntity, Is.Not.EqualTo(firstEntity));
            Assert.That(_world.IsEntityValid(nextEntity), Is.True);

            nextItem.ReturnToPool();

            Assert.That(_world.IsEntityValid(nextEntity), Is.False);
            AssertUnbound();
        }

        [Test]
        public void CanceledReusedCheckout_DoesNotDeleteAnEntityReusingItsOldId()
        {
            var item = _pool.Get();
            var originalId = _view.InitAsEntity(_world);
            item.ReturnToPool();
            var replacementId = _world.Create();
            _world.Add<Anchor>(replacementId);
            var replacement = _world.GetById(replacementId);
            Assert.That(replacementId, Is.EqualTo(originalId));

            _pool.GetAsync(0, (checkout, _) => checkout.ReturnToPool());

            Assert.That(_world.IsEntityValid(replacement), Is.True);
            AssertUnbound();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExternallyDeletedEntity_ReturnOrDestroyCannotDeleteItsReplacement(bool destroy)
        {
            var item = _pool.Get();
            var originalId = _view.InitAsEntity(_world);
            var originalEntity = _view.Entity;
            _world.Delete(originalId);
            var replacementId = _world.Create();
            _world.Add<Anchor>(replacementId);
            var replacement = _world.GetById(replacementId);
            Assert.That(replacementId, Is.EqualTo(originalId));
            Assert.That(replacement, Is.Not.EqualTo(originalEntity));

            if (destroy)
            {
                Object.DestroyImmediate(_poolRoot);
                _poolRoot = null;
            }
            else
            {
                item.ReturnToPool();
                AssertUnbound();
            }

            Assert.That(_world.IsEntityValid(replacement), Is.True);
        }

        private void AssertUnbound()
        {
            Assert.That(_view.IsValid, Is.False);
            Assert.That(_view.World, Is.Null);
            Assert.That(_view.Id, Is.EqualTo(-1));
            Assert.That(_view.Entity, Is.EqualTo(default(Entity)));
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
#endif
