using CodexECS;
using System.Collections.Generic;

namespace CodexFramework.Netwroking.Serialization
{
    public abstract class Serializer
    {
        protected Dictionary<ushort, Entity> _netIdToEntity;
        protected Dictionary<int, ushort> _entityIdToNetId;

        public Serializer()
        {
            _netIdToEntity = new();
            _entityIdToNetId = new();
        }

        protected Entity AddNetEntity(EcsWorld world, ushort netId)
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

#if DEBUG
        //TODO: add checks
        //TODO: cover use cases: Add, Remove, Create, Delete, Sync
        protected bool CheckNetIds(EcsWorld world)
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
