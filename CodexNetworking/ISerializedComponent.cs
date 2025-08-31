
using CodexECS;
using System;
using System.IO;

namespace CodexFramework.Netwroking.Serialization
{
    //crtp for enforcing ISerializedComponent to be IEquatable
    public interface ISerializedComponent<T> : IComponent, IEquatable<T>
    {
        public void Serialize(BinaryWriter writer);
        public void Deserialize(BinaryReader reader);
    }
}
