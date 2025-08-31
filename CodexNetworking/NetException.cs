using System;

namespace CodexFramework.Netwroking.Serialization
{
    public class NetException : Exception
    {
        public NetException(string msg) : base(msg) { }
    }
}
