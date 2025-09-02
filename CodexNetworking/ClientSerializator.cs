
using CodexECS;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace CodexFramework.Netwroking.Serialization.Client
{
    public class ClientSerializator : Serializator
    {
        private List<byte> _inputBuffer;

        public ClientSerializator() : base()
        {
            ClientSerializatorMapping.Init();

            _inputBuffer = new();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deserialize(EcsWorld world, ServerConnection connection)
        {
            var reader = connection.Reader;
            var dirtyCount = reader.ReadInt32();
            for (var i = 0; i < dirtyCount; i++)
                DeserializeComponents(world, reader);
            //for explicit reactive systems call after components added or removed on deserialization
            world.Unlock();
        }

        private void DeserializeComponents(EcsWorld world, BinaryReader reader)
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
                    var serializator = ClientSerializatorMapping.GetSerializator(componentId);
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

        public bool QueueInput(byte command)
        {
            if (_inputBuffer.Count >= byte.MaxValue)
                return false;
            
            _inputBuffer.Add(command);
            
            return true;
        }

        public void FlushInput(ServerConnection connection)
        {
            var writer = connection.Writer;
            writer.Write((byte)_inputBuffer.Count);
            for (int i = 0; i < _inputBuffer.Count; i++)
                writer.Write((byte)_inputBuffer[i]);
            _inputBuffer.Clear();
        }
    }
}
