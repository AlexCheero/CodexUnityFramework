#if !CODEX_UNITASK_SUPPORT
using System;
using CodexECS;
using CodexFramework.CodexEcsUnityIntegration.Views;
using UnityEngine;

namespace CodexFramework.Utils.Pools
{
    public static partial class PooledEntityViewsHelper
    {
        public static void GetPooledEntityViewAsync(PooledEntityView prototype, EcsWorld world) =>
            PoolManager.Instance.GetByPrototype(prototype.Item).GetAsync(world, OnInitPooledEntityView);

        public static void GetPooledEntityViewAsync(PooledEntityView prototype, EcsWorld world, Vector3 position) =>
            PoolManager.Instance.GetByPrototype(prototype.Item).GetAsync(position, world, OnInitPooledEntityView);

        public static void GetPooledEntityViewAsync(
            PooledEntityView prototype,
            EcsWorld world,
            Vector3 position,
            Quaternion rotation) =>
            PoolManager.Instance.GetByPrototype(prototype.Item).GetAsync(position, rotation, world, OnInitPooledEntityView);

        public static void GetPooledEntityViewAsync(PooledEntityView prototype, EcsWorld world, Action<EntityView> onReady) =>
            GetPooledEntityViewAsync(prototype, world, onReady, OnActionEntityViewReady);

        public static void GetPooledEntityViewAsync<TState>(
            PooledEntityView prototype,
            EcsWorld world,
            TState state,
            Action<EntityView, TState> onReady)
        {
            var pool = PoolManager.Instance.GetByPrototype(prototype.Item);
            var payload = new EntityViewSpawnPayload<TState>
            {
                World = world,
                State = state,
                OnReady = onReady
            };
            pool.GetAsync(payload, OnEntityViewSpawned);
        }

        public static void GetPooledEntityViewAsync(
            PooledEntityView prototype,
            EcsWorld world,
            Vector3 position,
            Action<EntityView> onReady) =>
            GetPooledEntityViewAsync(prototype, world, position, onReady, OnActionEntityViewReady);

        public static void GetPooledEntityViewAsync<TState>(
            PooledEntityView prototype,
            EcsWorld world,
            Vector3 position,
            TState state,
            Action<EntityView, TState> onReady)
        {
            var pool = PoolManager.Instance.GetByPrototype(prototype.Item);
            var payload = new EntityViewSpawnPayload<TState>
            {
                World = world,
                State = state,
                OnReady = onReady
            };
            pool.GetAsync(position, payload, OnEntityViewSpawned);
        }

        public static void GetPooledEntityViewAsync(
            PooledEntityView prototype,
            EcsWorld world,
            Vector3 position,
            Quaternion rotation,
            Action<EntityView> onReady) =>
            GetPooledEntityViewAsync(prototype, world, position, rotation, onReady, OnActionEntityViewReady);

        public static void GetPooledEntityViewAsync<TState>(
            PooledEntityView prototype,
            EcsWorld world,
            Vector3 position,
            Quaternion rotation,
            TState state,
            Action<EntityView, TState> onReady)
        {
            var pool = PoolManager.Instance.GetByPrototype(prototype.Item);
            var payload = new EntityViewSpawnPayload<TState>
            {
                World = world,
                State = state,
                OnReady = onReady
            };
            pool.GetAsync(position, rotation, payload, OnEntityViewSpawned);
        }
    }
}
#endif
