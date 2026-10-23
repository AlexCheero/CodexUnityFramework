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

        // The groupMask overloads place the entity in that allocation group (see EntityView.InitAsEntity).
        public static void GetPooledEntityViewAsync<TState>(
            PooledEntityView prototype,
            EcsWorld world,
            TState state,
            Action<EntityView, TState> onReady,
            in BitMask groupMask) =>
            PoolManager.Instance.GetByPrototype(prototype.Item)
                .GetAsync(GroupedPayload(world, state, onReady, in groupMask), EntityViewSpawnedCache<TState>.Action);

        public static void GetPooledEntityViewAsync<TState>(
            PooledEntityView prototype,
            EcsWorld world,
            Vector3 position,
            TState state,
            Action<EntityView, TState> onReady,
            in BitMask groupMask) =>
            PoolManager.Instance.GetByPrototype(prototype.Item)
                .GetAsync(position, GroupedPayload(world, state, onReady, in groupMask), EntityViewSpawnedCache<TState>.Action);

        public static void GetPooledEntityViewAsync<TState>(
            PooledEntityView prototype,
            EcsWorld world,
            Vector3 position,
            Quaternion rotation,
            TState state,
            Action<EntityView, TState> onReady,
            in BitMask groupMask) =>
            PoolManager.Instance.GetByPrototype(prototype.Item).GetAsync(
                position, rotation, GroupedPayload(world, state, onReady, in groupMask), EntityViewSpawnedCache<TState>.Action);

        private static EntityView InitView(PoolItem item, EcsWorld world)
        {
            if (item == null)
                return null;
            var view = item.GetComponentAndCache<EntityView>();
            view.InitAsEntity(world);
            return view;
        }

        private static EntityView InitView(PoolItem item, EcsWorld world, in BitMask groupMask)
        {
            if (item == null)
                return null;
            var view = item.GetComponentAndCache<EntityView>();
            view.InitAsEntity(world, in groupMask);
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
            // Without a group the entity is placed by the view's full component mask.
            public bool Grouped;
            public BitMask Group;
        }

        private static EntityViewSpawnPayload<TState> GroupedPayload<TState>(
            EcsWorld world, TState state, Action<EntityView, TState> onReady, in BitMask groupMask) =>
            new() { World = world, State = state, OnReady = onReady, Grouped = true, Group = groupMask };

        private static class EntityViewSpawnedCache<TState>
        {
            public static readonly Action<PoolItem, EntityViewSpawnPayload<TState>> Action = OnEntityViewSpawned;
        }

        private static void OnEntityViewSpawned<TState>(PoolItem item, EntityViewSpawnPayload<TState> payload)
        {
            var view = payload.Grouped ? InitView(item, payload.World, in payload.Group) : InitView(item, payload.World);
            if (view != null)
                payload.OnReady?.Invoke(view, payload.State);
        }

        private static readonly Action<EntityView, Action<EntityView>> OnActionEntityViewReadyAction = OnActionEntityViewReady;

        private static void OnActionEntityViewReady(EntityView view, Action<EntityView> onReady) =>
            onReady?.Invoke(view);
    }
}
