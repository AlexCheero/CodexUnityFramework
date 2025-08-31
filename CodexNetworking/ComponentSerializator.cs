
using CodexECS;
using System.IO;
using System.Runtime.CompilerServices;

namespace CodexFramework.Netwroking.Serialization
{
    public interface IComponentSerializator
    {
        public void Serialize(int eid, EcsWorld world, BinaryWriter writer);
        public void Deserialize(int eid, EcsWorld world, BinaryReader reader);
        public void UpdateSnapshot(int eid, EcsWorld world);
        public bool IsDirty(int eid, EcsWorld world);
    }

    public class ComponentSerializator<T> : IComponentSerializator where T : struct, ISerializedComponent<T>
    {
        public void Serialize(int eid, EcsWorld world, BinaryWriter writer)
        {
#if DEBUG
            if (!IsDirty(eid, world))
                throw new NetException($"trying to serialize not dirty component {typeof(T).Name}");
#endif

            var haveComponent = world.Have<T>(eid);
            var haveSnapshot = world.Have<Snapshot<T>>(eid) && world.Get<Snapshot<T>>(eid).val.HasValue;

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

        public void Deserialize(int eid, EcsWorld world, BinaryReader reader)
        {
            var haveRemote = reader.ReadBoolean();
            var haveLocal = world.Have<T>(eid);
            if (haveRemote)
            {
                if (!haveLocal)
                    world.Add<T>(eid);
                world.Get<T>(eid).Deserialize(reader);
            }
            else if (haveLocal)
            {
                world.Remove<T>(eid);
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
