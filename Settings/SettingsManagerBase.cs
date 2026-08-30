using System.Collections.Generic;
using CodexFramework.Utils;
using UnityEngine;

namespace CodexFramework.Settings
{
    public abstract class SettingsManagerBase<TSettings, TSerializer>
        where TSettings : IGameSettings, new()
        where TSerializer : ISettingsSerializer, new()
    {
        private static readonly TSerializer Serializer = new TSerializer();
        private static Dictionary<string, string> _storage;

        public static TSettings UserSettings;

        public static void Load()
        {
            UserSettings = new TSettings();
            var meta = UserSettings.Persistence;
            if (PlayerPrefs.HasKey(meta.StorageKey))
            {
                var serializedStorage = PlayerPrefs.GetString(meta.StorageKey);
                _storage = Serializer.Deserialize<Dictionary<string, string>>(serializedStorage);
            }
            _storage ??= new Dictionary<string, string>();

            if (ShouldWipe(meta))
                Wipe(meta);

            UserSettings.Load(_storage, Serializer);
        }

        public static void Save()
        {
            _storage ??= new Dictionary<string, string>();
            UserSettings.Save(_storage, Serializer);

            var meta = UserSettings.Persistence;
            var serializedStorage = Serializer.Serialize(_storage);
            PlayerPrefs.SetString(meta.StorageKey, serializedStorage);
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