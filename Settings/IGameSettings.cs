using System.Collections.Generic;

namespace CodexFramework.Settings
{
    /// <summary>
    /// Package-neutral text serialization used by the settings framework. Concrete JSON (or
    /// other format) implementations belong to the consuming game or an optional integration.
    /// </summary>
    public interface ISettingsSerializer
    {
        string Serialize<T>(T value);
        T Deserialize<T>(string serializedValue);
    }

    public interface IGameSettings
    {
        SettingsPersistenceMeta Persistence { get; }

        void Load(Dictionary<string, string> storage, ISettingsSerializer serializer);
        void Save(Dictionary<string, string> storage, ISettingsSerializer serializer);
        void ResetToDefaults();
    }
}
