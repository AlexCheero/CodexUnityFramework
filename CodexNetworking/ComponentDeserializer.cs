using CodexECS;
using System.IO;

namespace CodexFramework.Netwroking.Serialization.Client
{
    public interface IComponentDeserializer
    {
        public void Deserialize(int eid, EcsWorld world, BinaryReader reader);
    }

    public class ComponentDeserializer<T> : IComponentDeserializer
        where T : struct, ISerializedComponent<T>
    {
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
}