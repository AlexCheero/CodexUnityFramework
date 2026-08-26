using System.Collections.Generic;
using CodexFramework.Utils;
using Newtonsoft.Json;
using UnityEngine;

namespace CodexFramework.Settings
{
    public abstract class SettingsManagerBase<TSettings> where TSettings : IGameSettings, new()
    {
        //TODO: probably should make this local vars
        private static Dictionary<string, string> _storage;
        private static JsonSerializerSettings _serializerSettings;

        public static TSettings UserSettings;

        public static void Load()
        {
            _serializerSettings = new JsonSerializerSettings
            {
                Error = (_, args) =>
                {
                    Debug.Log(args.ErrorContext.Error.Message);
                    args.ErrorContext.Handled = true;
                }
            };

            UserSettings = new TSettings();
            var meta = UserSettings.Persistence;
            if (PlayerPrefs.HasKey(meta.StorageKey))
            {
                var json = PlayerPrefs.GetString(meta.StorageKey);
                _storage = JsonConvert.DeserializeObject<Dictionary<string, string>>(json, _serializerSettings);
            }
            _storage ??= new Dictionary<string, string>();

            if (ShouldWipe(meta))
                Wipe(meta);

            UserSettings.Load(_storage, _serializerSettings);
        }

        public static void Save()
        {
            _storage ??= new Dictionary<string, string>();
            UserSettings.Save(_storage, _serializerSettings);

            var meta = UserSettings.Persistence;
            var json = JsonConvert.SerializeObject(_storage);
            PlayerPrefs.SetString(meta.StorageKey, json);
            PlayerPrefs.SetString(meta.VersionKey, Application.version);
            PlayerPrefs.Save();
        }

        private static bool ShouldWipe(SettingsPersistenceMeta meta)
        {
            var storedVersion = GetStoredSettingsVersion(meta);
            return storedVersion != null &&
                   VersionUtility.CompareVersions(storedVersion, meta.WipeBelowVersion) < 0;
        }

        private static string GetStoredSettingsVersion(SettingsPersistenceMeta meta)
        {
            if (PlayerPrefs.HasKey(meta.VersionKey))
                return PlayerPrefs.GetString(meta.VersionKey);
            if (PlayerPrefs.HasKey(meta.LegacyVersionKey))
                return PlayerPrefs.GetString(meta.LegacyVersionKey);
            return null;
        }

        private static void Wipe(SettingsPersistenceMeta meta)
        {
            _storage.Clear();
            PlayerPrefs.DeleteKey(meta.StorageKey);
            PlayerPrefs.DeleteKey(meta.VersionKey);
            PlayerPrefs.DeleteKey(meta.LegacyVersionKey);
            UserSettings.ResetToDefaults();
        }
    }
}