using CodexECS;
using System.IO;
using System.Runtime.CompilerServices;

namespace CodexFramework.Netwroking.Serialization.Server
{
    public interface IComponentSerializer
    {
        public void Serialize(int eid, EcsWorld world, BinaryWriter writer);
        public void UpdateSnapshot(int eid, EcsWorld world);
        public bool IsDirty(int eid, EcsWorld world);
    }

    public class ComponentSerializer<T> : IComponentSerializer
        where T : struct, ISerializedComponent<T>
    {
        public void Serialize(int eid, EcsWorld world, BinaryWriter writer)
        {
#if DEBUG
            if (!IsDirty(eid, world))
                throw new NetException($"trying to serialize not dirty component {typeof(T).Name}");
#endif

            var haveComponent = world.Have<T>(eid);
            var haveSnapshot = world.Have<Snapshot<T>>(eid) && world.Get<Snapshot<T>>(eid).val.HasValue;

            //TODO: IsDirty works implicitly, looks like we could skip both if branches
            if (haveComponent)
            {
                writer.Write(true);
                world.Get<T>(eid).Serialize(writer);
            }
            else if (haveSnapshot)
            {
                writer.Write(false);
            }
        }

        public void UpdateSnapshot(int eid, EcsWorld world)
        {
            if (!world.Have<Snapshot<T>>(eid))
                world.Add<Snapshot<T>>(eid);

            world.Get<Snapshot<T>>(eid).val = world.Have<T>(eid) ? world.Get<T>(eid) : null;
        }

        public bool IsDirty(int eid, EcsWorld world)
        {
            ref readonly var snapshot = ref GetSnapshot(eid, world);
            if (world.Have<T>(eid))
                return !snapshot.HasValue || world.Get<T>(eid).Equals(snapshot.Value);
            else
                return snapshot.HasValue;
        }

        private static readonly T? _emptySnapshot = null;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ref readonly T? GetSnapshot(int eid, EcsWorld world)
        {
            if (world.Have<Snapshot<T>>(eid))
                return ref world.Get<Snapshot<T>>(eid).val;
            return ref _emptySnapshot;
        }
    }
}
