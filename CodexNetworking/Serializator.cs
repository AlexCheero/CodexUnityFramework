
using CodexECS;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace CodexFramework.Netwroking.Serialization
{
    public class Serializator
    {
        private ushort _nextNetId;
        private SimpleList<ushort> _freeIds;
        private Dictionary<ushort, Entity> _netIdToEntity;
        private Dictionary<int, ushort> _entityIdToNetId;
        private EcsFilter _netIdsFilter;

        public Serializator(EcsWorld world)
        {
            SerializatorMapping.Init();

            _netIdToEntity = new();
            _entityIdToNetId = new();
            _freeIds = new();

            _netIdsFilter = world.Filter()
                .With<NetId>()
                .Build();
        }

        public void Serialize(EcsWorld world, ClientConnection connection)
        {
            var entityTuples = connection.Entities;
            for (int i = 0; i < entityTuples.Length; i++)
            {
                var tuple = entityTuples.GetNthValue(i);
                SerializeComponents(tuple.E, tuple.Components, world, connection.Writer);
            }
        }

        public void Deserialize(EcsWorld world, BinaryReader reader)
        {
            var dirtyCount = reader.ReadInt32();
            for (var i = 0; i < dirtyCount; i++)
                DeserializeComponents(world, reader);
            //for explicit reactive systems call after components added or removed on deserialization
            world.Unlock();
        }

        public Entity CreateNetEntity(EcsWorld world)
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

            var entity = AddNetEntity(world, newNetId);

            return entity;
        }

        private Entity AddNetEntity(EcsWorld world, ushort netId)
        {
#if DEBUG
            if (_netIdToEntity.ContainsKey(netId))
                throw new NetException("Already have this net id");
#endif
            var eid = world.Create();
            _netIdToEntity[netId] = world.GetById(eid);

#if DEBUG
            if (_entityIdToNetId.ContainsKey(eid))
                throw new NetException($"{nameof(_entityIdToNetId)} already have newly created eid");
#endif

            _entityIdToNetId[eid] = netId;
            world.Add(eid, new NetId { id = netId });

            return world.GetById(eid);
        }

        public void UpdateSnapshots(EcsWorld world)
        {
            foreach (var netComponentId in SerializatorMapping.SerializedComponents)
            {
                var serializator = SerializatorMapping.GetSerializator(netComponentId);
                foreach (var eid in _netIdsFilter)
                {
                    serializator.UpdateSnapshot(eid, world);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializeComponents(Entity entity, in BitMask componentsMask
            , EcsWorld world, BinaryWriter writer)
        {
            var eid = entity.GetId();
#if DEBUG
            if (!_entityIdToNetId.ContainsKey(eid))
                throw new NetException($"{nameof(_entityIdToNetId)} have no eid {eid}");
#endif
            var netId = _entityIdToNetId[eid];
            if (!world.IsEntityValid(entity))
            {
                writer.Write(netId);
                writer.Write((short)-1);

                _netIdToEntity.Remove(netId);
                _entityIdToNetId.Remove(eid);

                return;
            }

            if (!world.Have<NetDirty>(eid))
                return;

#if DEBUG
            if (!world.Have<NetId>(eid))
                throw new NetException("entity have no net id component");
            if (world.Get<NetId>(eid).id != netId)
                throw new NetException("net id desync");
#endif

            writer.Write(netId);

            var countPos = writer.BaseStream.Position;
            writer.Write((short)0);

            short componentsCount = 0;
            foreach (var componentId in componentsMask)
            {
                var serializator = SerializatorMapping.GetSerializator(componentId);
                if (!serializator.IsDirty(eid, world))
                    continue;

                writer.Write((ushort)componentId);
                serializator.Serialize(eid, world, writer);
                componentsCount++;
            }

#if DEBUG
            if (componentsCount == 0)
                throw new NetException($"{nameof(SerializeComponents)}: {nameof(componentsCount)} is 0");
#endif

            long endPos = writer.BaseStream.Position;
            writer.BaseStream.Position = countPos;
            writer.Write(componentsCount);
            writer.BaseStream.Position = endPos;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DeserializeComponents(EcsWorld world, BinaryReader reader)
        {
            var netId = reader.ReadUInt16();
            Entity entity;
            int eid;
            if (_netIdToEntity.ContainsKey(netId))
            {
                entity = _netIdToEntity[netId];
                eid = entity.GetId();
#if DEBUG
                if (world.GetById(eid).Val != _netIdToEntity[netId].Val || netId != _entityIdToNetId[eid])
                    throw new NetException($"{nameof(_entityIdToNetId)} mapping desync");
#endif
            }
            else
            {
                entity = AddNetEntity(world, netId);
                eid = entity.GetId();
            }

            var componentsCount = reader.ReadInt16();

#if DEBUG
            if (componentsCount == 0)
                throw new NetException($"{nameof(DeserializeComponents)}: {nameof(componentsCount)} is 0");
#endif

            if (componentsCount > 0)
            {
                for (int i = 0; i < componentsCount; i++)
                {
                    var componentId = reader.ReadUInt16();
                    var serializator = SerializatorMapping.GetSerializator(componentId);
                    serializator.Deserialize(eid, world, reader);
                }

#if DEBUG
                if (!world.IsEntityValid(entity) || world.GetMask(eid).SetBitsCount == 0)
                    throw new NetException("at least NetId component should left on entity");
#endif
            }
            else //if (componentsCount < 0)
            {
                world.Delete(eid);
                _netIdToEntity.Remove(netId);
                _entityIdToNetId.Remove(eid);
            }
        }

#if DEBUG
        //TODO: add checks
        //TODO: cover use cases: Add, Remove, Create, Delete, Sync
        //TODO: split into server and client
        private bool CheckNetIds(EcsWorld world)
        {
            foreach (var pair in _netIdToEntity)
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

            foreach (var pair in _entityIdToNetId)
            {
                var eid = pair.Key;
                if (!world.IsIdValid(eid))
                    return false;

                if (!world.Have<NetId>(eid))
                    return false;

                if (_netIdToEntity.ContainsKey(pair.Value))
                    return false;

                if (_netIdToEntity[pair.Value].Val != world.GetById(eid).Val)
                    return false;
            }

            return true;
        }
#endif
    }
}
