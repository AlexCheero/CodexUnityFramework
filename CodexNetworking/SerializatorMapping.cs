
using CodexECS;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace CodexFramework.Netwroking
{
    public class NetException : Exception
    {
        public NetException(string msg) : base(msg) { }
    }

    public enum ENetCommand
    {
        Sync,
        Delete,

        End
    }

    public struct NetId : IComponent
    {
        public ushort id;
    }

    public interface ISerializedComponent : IComponent
    {
        public void Serialize(BinaryWriter writer);
        public void Deserialize(BinaryReader reader);
    }

    public interface ISerializator
    {
        public void Serialize(int eid, EcsWorld world, BinaryWriter writer);
        public void Deserialize(int eid, EcsWorld world, BinaryReader reader);
    }

    public class Serializator<T> : ISerializator where T : struct, ISerializedComponent
    {
        public void Serialize(int eid, EcsWorld world, BinaryWriter writer)
        {
            var haveComponent = world.Have<T>(eid);
            writer.Write(haveComponent);
            if (haveComponent)
                world.Get<T>(eid).Serialize(writer);
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
    }

    public static class SerializatorMapping
    {
        private static Dictionary<Type, ISerializator> _serializators = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SerializeComponents(int eid, in BitMask components, EcsWorld world, BinaryWriter writer)
        {
            writer.Write(world.Get<NetId>(eid).id);
            writer.Write((ushort)components.SetBitsCount);
            foreach (var componentId in components)
            {
                writer.Write((ushort)componentId);
                var serializator = GetSerializator(componentId);
                serializator.Serialize(eid, world, writer);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DeserializeComponents(EcsWorld world, BinaryReader reader)
        {
            //TODO: what to do if there is no net components left on entity?

            var netId = reader.ReadUInt16();
            var eid = GetEntityFromNetId(netId).GetId();
            var componentsCount = reader.ReadUInt16();
            for (int i = 0; i < componentsCount; i++)
            {
                var componentId = reader.ReadUInt16();
                var serializator = GetSerializator(componentId);
                serializator.Deserialize(eid, world, reader);
            }
        }

        public static int CreateNetEntity(EcsWorld world)
        {
            var nextNetId = (ushort)_netIdToEntityId.Length;
            return AddNetEntity(world, nextNetId);
        }

        public static int AddNetEntity(EcsWorld world, ushort netId)
        {
#if DEBUG
            if (_netIdToEntityId.Length == ushort.MaxValue)
                throw new NetException("Net Id overflow");
            if (HaveNetId(netId))
                throw new NetException("Already have this net id");
#endif
            var eid = world.Create();
            if (eid >= _netIdToEntityId.Length)
                _netIdToEntityId.Resize(eid);
            _netIdToEntityId.Add(world.GetById(eid));
            world.Add(eid, new NetId { id = netId });

            return eid;
        }

        private static SimpleList<Entity> _netIdToEntityId;
        static SerializatorMapping()
        {
            _netIdToEntityId = new();
            //first element is always invalid, because net ids should start from 1
            _netIdToEntityId.Add(EntityExtension.NullEntity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Entity GetEntityFromNetId(ushort netId) => _netIdToEntityId[netId];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HaveNetId(ushort netId) =>
            netId < _netIdToEntityId.Length && !_netIdToEntityId[netId].IsNull();

        public static void DeleteNetEntityByEid(Entity entity, EcsWorld world)
        {
            var eid = entity.GetId();
            var netId = world.Get<NetId>(eid).id;

#if DEBUG
            if (!HaveNetId(netId))
                throw new NetException("no net id found to delete");
            if (entity.Val != _netIdToEntityId[netId].Val)
                throw new NetException("trying to delete invalid entity");
#endif

            DeleteNetEntityByNetId(netId, world);
        }

        public static void DeleteNetEntityByNetId(ushort netId, EcsWorld world)
        {
            var last = _netIdToEntityId[^1];
            world.Get<NetId>(last.GetId()).id = netId;
            _netIdToEntityId.SwapRemoveAt(netId);

#if DEBUG
            if (!CheckNetIds(world))
                throw new NetException("net ids desync after deletion");
#endif
        }

        //TODO: serialize deletion for multiple entities
        public static void DeserializeDeleteEntities(EcsWorld world, BinaryReader reader)
        {

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ISerializator GetSerializator(int componentId)
        {
            var type = ComponentMapping.GetTypeForId(componentId);
            if (!_serializators.ContainsKey(type))
            {
                var closedType = typeof(Serializator<>).MakeGenericType(type);
                _serializators[type] = (ISerializator)Activator.CreateInstance(closedType);
            }

            return _serializators[type];
        }

#if DEBUG
        private static bool CheckNetIds(EcsWorld world)
        {
            for (int i = 1; i < _netIdToEntityId.Length; i++)
            {
                var entity = _netIdToEntityId[i];
                if (!world.IsEntityValid(entity))
                    return false;

                var eid = entity.GetId();
                if (!world.Have<NetId>(eid))
                    return false;

                if (world.Get<NetId>(eid).id != i)
                    return false;
            }

            return true;
        }
#endif
    }
}
