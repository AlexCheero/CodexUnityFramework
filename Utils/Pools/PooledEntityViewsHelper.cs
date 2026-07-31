using System;
using CodexECS;
using CodexFramework.CodexEcsUnityIntegration.Views;
using UnityEngine;

namespace CodexFramework.Utils.Pools
{
    public static partial class PooledEntityViewsHelper
    {
        public static EntityView GetPooledEntityView(PooledEntityView prototype, EcsWorld world)
        {
            var pool = PoolManager.Instance.GetByPrototype(prototype.Item);
            var view = pool.Get().GetComponentAndCache<EntityView>();
            view.InitAsEntity(world);
            return view;
        }

        public static EntityView GetPooledEntityView(PooledEntityView prototype, EcsWorld world, Vector3 position)
        {
            var pool = PoolManager.Instance.GetByPrototype(prototype.Item);
            var view = pool.Get(position).GetComponentAndCache<EntityView>();
            view.InitAsEntity(world);
            return view;
        }

        public static EntityView GetPooledEntityView(PooledEntityView prototype, EcsWorld world, Vector3 position, Quaternion rotation)
        {
            var pool = PoolManager.Instance.GetByPrototype(prototype.Item);
            var view = pool.Get(position, rotation).GetComponentAndCache<EntityView>();
            view.InitAsEntity(world);
            return view;
        }

        private static EntityView InitView(PoolItem item, EcsWorld world)
        {
            if (item == null)
                return null;
            var view = item.GetComponentAndCache<EntityView>();
            view.InitAsEntity(world);
            return view;
        }

        private static readonly Action<PoolItem, EcsWorld> OnInitPooledEntityViewAction = OnInitPooledEntityView;

        private static void OnInitPooledEntityView(PoolItem item, EcsWorld world) =>
            InitView(item, world);

        private struct EntityViewSpawnPayload<TState>
        {
            public EcsWorld World;
            public TState State;
            public Action<EntityView, TState> OnReady;
        }

        private static class EntityViewSpawnedCache<TState>
        {
            public static readonly Action<PoolItem, EntityViewSpawnPayload<TState>> Action = OnEntityViewSpawned;
        }

        private static void OnEntityViewSpawned<TState>(PoolItem item, EntityViewSpawnPayload<TState> payload)
        {
            var view = InitView(item, payload.World);
            if (view != null)
                payload.OnReady?.Invoke(view, payload.State);
        }

        private static readonly Action<EntityView, Action<EntityView>> OnActionEntityViewReadyAction = OnActionEntityViewReady;

        private static void OnActionEntityViewReady(EntityView view, Action<EntityView> onReady) =>
            onReady?.Invoke(view);
    }
}
