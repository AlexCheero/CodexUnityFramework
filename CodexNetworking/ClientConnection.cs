using CodexECS;
using System.IO;

namespace CodexFramework.Netwroking.Serialization
{
    public class ClientConnection
    {
        public BinaryWriter Writer;
        public BinaryReader Reader;

        public struct EntityComponents
        {
            public Entity E;
            public BitMask Components;
        }

        public SparseSet<EntityComponents> Entities;

        public ClientConnection()
        {
            Entities = new();
        }

        public void AddSyncedEntity(Entity entity, BitMask mask = default)
        {
            var eid = entity.GetId();
            if (Entities.ContainsIdx(eid))
                return;
            Entities.Add(eid, new EntityComponents
            {
                E = entity,
                Components = mask
            });
        }

        public void SyncComponent(int eid, int componentId) => Entities[eid].Components.Set(componentId);
        public void SyncComponents(int eid, in BitMask mask) => Entities[eid].Components.Set(mask);
        public void UnsyncComponent(int eid, int componentId) => Entities[eid].Components.Unset(componentId);
    }
}
