namespace CodexFramework.Netwroking.Serialization
{
    public struct Snapshot<T> where T : struct, ISerializedComponent<T>
    {
        public T? val;
    }
}
