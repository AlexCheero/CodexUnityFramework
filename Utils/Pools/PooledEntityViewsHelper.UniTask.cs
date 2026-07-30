#if CODEX_UNITASK_SUPPORT
using System;
using CodexECS;
using CodexFramework.CodexEcsUnityIntegration.Views;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CodexFramework.Utils.Pools
{
    public static partial class PooledEntityViewsHelper
    {
        public static async UniTask<EntityView> GetPooledEntityViewAsync(PooledEntityView prototype, EcsWorld world)
        {
            var pool = PoolManager.Instance.GetByPrototype(prototype.Item);
            var item = await pool.GetAsync();
            return InitView(item, world);
        }

        public static async UniTask<EntityView> GetPooledEntityViewAsync(
            PooledEntityView prototype,
            EcsWorld world,
            Vector3 position)
        {
            var pool = PoolManager.Instance.GetByPrototype(prototype.Item);
            var item = await pool.GetAsync(position);
            return InitView(item, world);
        }

        public static async UniTask<EntityView> GetPooledEntityViewAsync(
            PooledEntityView prototype,
            EcsWorld world,
            Vector3 position,
            Quaternion rotation)
        {
            var pool = PoolManager.Instance.GetByPrototype(prototype.Item);
            var item = await pool.GetAsync(position, rotation);
            return InitView(item, world);
        }

        public static void GetPooledEntityViewAsync(PooledEntityView prototype, EcsWorld world, Action<EntityView> onReady) =>
            GetPooledEntityViewAsyncInternal(prototype, world, onReady).Forget();

        private static async UniTaskVoid GetPooledEntityViewAsyncInternal(
            PooledEntityView prototype,
            EcsWorld world,
            Action<EntityView> onReady)
        {
            var view = await GetPooledEntityViewAsync(prototype, world);
            onReady?.Invoke(view);
        }

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
            GetPooledEntityViewAsyncInternal(prototype, world, position, onReady).Forget();

        private static async UniTaskVoid GetPooledEntityViewAsyncInternal(
            PooledEntityView prototype,
            EcsWorld world,
            Vector3 position,
            Action<EntityView> onReady)
        {
            var view = await GetPooledEntityViewAsync(prototype, world, position);
            onReady?.Invoke(view);
        }

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
            GetPooledEntityViewAsyncInternal(prototype, world, position, rotation, onReady).Forget();

        private static async UniTaskVoid GetPooledEntityViewAsyncInternal(
            PooledEntityView prototype,
            EcsWorld world,
            Vector3 position,
            Quaternion rotation,
            Action<EntityView> onReady)
        {
            var view = await GetPooledEntityViewAsync(prototype, world, position, rotation);
            onReady?.Invoke(view);
        }

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
