
using CodexECS;
using System.Collections.Generic;

namespace CodexFramework.Netwroking.Serialization.Server
{
    public struct ClientNetInput : IComponent
    {
        public List<byte> buffer;

        public static void Init(ref ClientNetInput instance) => instance.buffer ??= new();
        public static void Cleanup(ref ClientNetInput instance) => instance.buffer?.Clear();
    }
}
