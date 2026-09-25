using System.Collections.Generic;

namespace CodexFramework.Saves
{
    public interface ISaveCloudStorage
    {
        bool Enabled { get; }
        IEnumerable<string> GetFileNames();
        byte[] Read(string fileName);
        void Write(string fileName, byte[] bytes);
        void Delete(string fileName);
    }
}
