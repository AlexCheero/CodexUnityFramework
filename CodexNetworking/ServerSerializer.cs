using CodexECS;
using System.IO;
using System.Runtime.CompilerServices;

namespace CodexFramework.Netwroking.Serialization.Server
{
    public class ServerSerializer : Serializer
    {
        private ushort _nextNetId;
        private SimpleList<ushort> _freeIds;
        private EcsFilter _netIdsFilter;

        public ServerSerializer(EcsWorld world) : base()
        {
            ServerSerializerMapping.Init();

            _freeIds = new();

            _netIdsFilter = world.Filter()
                .With<NetId>()
                .Build();
        }

        public void UpdateSnapshots(EcsWorld world)
        {
            foreach (var netComponentId in SerializerMapping.SerializedComponents)
            {
                var serializator = ServerSerializerMapping.GetSerializer(netComponentId);
                foreach (var eid in _netIdsFilter)
                {
                    serializator.UpdateSnapshot(eid, world);
                }
            }
        }

        public void Serialize(EcsWorld world, ClientConnection connection)
        {
            var entityTuples = connection.Entities;
            connection.Writer.Write(entityTuples.Length);
            for (int i = 0; i < entityTuples.Length; i++)
            {
                var tuple = entityTuples.GetNthValue(i);
                SerializeComponents(tuple.E, tuple.Components, world, connection.Writer);
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

            //TODO: is never used!!!
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
                var serializer = ServerSerializerMapping.GetSerializer(componentId);
                if (!serializer.IsDirty(eid, world))
                    continue;

                writer.Write((ushort)componentId);
                serializer.Serialize(eid, world, writer);
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

        public void DeserializeClientInput(EcsWorld world, ClientConnection connection)
        {
            var eid = connection.ClientEntity.GetId();
#if DEBUG
            if (!world.Have<ClientNetInput>(eid))
                throw new NetException($"{nameof(ServerSerializer)}.{nameof(DeserializeClientInput)}: " +
                    $"player entity have no {nameof(ClientNetInput)} component");
#endif
            var inputBuffer = world.Get<ClientNetInput>(eid).buffer;

#if DEBUG
            //TODO: clean input buffer somewhere
            if (inputBuffer.Count > 0)
                throw new NetException($"{nameof(ServerSerializer)}.{nameof(DeserializeClientInput)}: " +
                    $"{nameof(inputBuffer)} shoud be empty at this point");
#endif

            var count = connection.Reader.ReadByte();
            for (int i = 0; i < count; i++)
                inputBuffer.Add(connection.Reader.ReadByte());
        }
    }
}
