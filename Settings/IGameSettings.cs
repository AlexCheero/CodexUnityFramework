using System.Collections.Generic;
using Newtonsoft.Json;

namespace CodexFramework.Settings
{
    public interface IGameSettings
    {
        SettingsPersistenceMeta Persistence { get; }

        void Load(Dictionary<string, string> storage, JsonSerializerSettings jsonSettings);
        void Save(Dictionary<string, string> storage, JsonSerializerSettings jsonSettings);
        void ResetToDefaults();
    }
}
