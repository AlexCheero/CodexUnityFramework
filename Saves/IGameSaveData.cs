using System;

namespace CodexFramework.Saves
{
    public interface IGameSaveData<T>
    {
        SavePersistenceMeta Persistence { get; }

        string Name { get; }
        int Position { get; set; }
        string LastSaveDateTime { get; set; }

        string ToJson();
        bool ContentEquals(T other);
        T WithPosition(int position);
        DateTime GetLastSaveUtc();
        void StampSaveTime();
    }

    public interface ISaveDataSerializer<T>
    {
        T FromJson(string json);
    }
}
