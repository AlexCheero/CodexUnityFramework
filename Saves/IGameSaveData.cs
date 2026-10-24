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
        DateTime GetLastSaveUtc();
        void StampSaveTime();
    }

    public interface ISaveDataSerializer<T>
    {
        T FromJson(string json);
    }

    // Optional game-owned conversion. Implementations must reject unsupported data without
    // side effects; storage commits only after every local/cloud candidate loads successfully.
    public interface ISaveDataMigration
    {
        string Migrate(string json);
    }
}
