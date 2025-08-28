
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
            //TODO: if there are some reactive systems on Add/Remove components, we should trigger them explicitly

            var netId = reader.ReadUInt16();
            var eid = _netIdToEntityId.ContainsKey(netId)
                ? _netIdToEntityId[netId].GetId()
                : AddNetEntity(world, netId);
            var componentsCount = reader.ReadUInt16();
            for (int i = 0; i < componentsCount; i++)
            {
                var componentId = reader.ReadUInt16();
                var serializator = GetSerializator(componentId);
                serializator.Deserialize(eid, world, reader);
            }
        }

        private static ushort _nextNetId;
        private static SimpleList<ushort> _freeIds;
        public static int CreateNetEntity(EcsWorld world)
        {
            ushort newNetId;
            var freeIdsLength = _freeIds.Length;
            if (freeIdsLength > 0)
            {
                var lastFreeIdsIdx = freeIdsLength - 1;
                newNetId = _freeIds[lastFreeIdsIdx];
                _freeIds.SwapRemoveAt(lastFreeIdsIdx);
            }
            else
            {
#if DEBUG
                if (_nextNetId == ushort.MaxValue)
                    throw new NetException("Net Id overflow");
#endif
                newNetId = _nextNetId;
                _nextNetId++;
            }

            var eid = AddNetEntity(world, newNetId);
            
            return eid;
        }

        public static int AddNetEntity(EcsWorld world, ushort netId)
        {
#if DEBUG
            if (_netIdToEntityId.ContainsKey(netId))
                throw new NetException("Already have this net id");
#endif
            var eid = world.Create();
            _netIdToEntityId[netId] = world.GetById(eid);
            world.Add(eid, new NetId { id = netId });

            return eid;
        }

        private static Dictionary<ushort, Entity> _netIdToEntityId;
        static SerializatorMapping()
        {
            _netIdToEntityId = new();
            _pendingDelete = new();
            _freeIds = new();
        }

        private static SimpleList<ushort> _pendingDelete;
        public static void EnqueueDeleteByNedId(ushort netId, EcsWorld world)
        {
#if DEBUG
            var entity = _netIdToEntityId[netId];
            var eid = entity.GetId();
            if (!world.Have<NetId>(eid) || world.Get<NetId>(eid).id != netId)
                throw new NetException("trying to delete invalid entity");

            for (int i = 0; i < _pendingDelete.Length; i++)
            {
                if (_pendingDelete[i] == netId)
                    throw new NetException("net id already pending delete");
            }
#endif

            _pendingDelete.Add(netId);
        }

        public static void EnqueueDelete(Entity entity, EcsWorld world)
        {
            var eid = entity.GetId();
            var netId = world.Get<NetId>(eid).id;

#if DEBUG
            if (!_netIdToEntityId.ContainsKey(netId))
                throw new NetException("no net id found to delete");
            if (entity.Val != _netIdToEntityId[netId].Val)
                throw new NetException("trying to delete invalid entity");
#endif

            EnqueueDeleteByNedId(netId, world);
        }

        private static void FlushDelete()
        {
            //serialize number of deleted entities

            for (int i = 0; i < _pendingDelete.Length; i++)
            {
                //serialize net ids
                _netIdToEntityId.Remove(_pendingDelete[i]);
                _freeIds.Add(_pendingDelete[i]);
            }

            _pendingDelete.Clear();
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
            foreach (var pair in _netIdToEntityId)
            {
                var netId = pair.Key;
                var entity = pair.Value;
                if (!world.IsEntityValid(entity))
                    return false;

                var eid = entity.GetId();
                if (!world.Have<NetId>(eid))
                    return false;

                if (world.Get<NetId>(eid).id != netId)
                    return false;
            }

            return true;
        }
#endif
    }
}
