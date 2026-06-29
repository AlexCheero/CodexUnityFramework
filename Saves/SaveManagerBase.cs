using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CodexFramework.Saves
{
    public abstract class SaveManagerBase<TSaveData, TSerializer>
        where TSaveData : IGameSaveData<TSaveData>, new()
        where TSerializer : ISaveDataSerializer<TSaveData>, new()
    {
        private static readonly TSerializer Serializer = default;

        private static List<TSaveData> _saves;
        private static SavePersistenceMeta _meta;

        public static IReadOnlyList<TSaveData> Saves => _saves;

        public static void LoadSaves()
        {
            _meta = new TSaveData().Persistence;
            if (ShouldWipe())
                Wipe();

            _saves ??= new();
            _saves.Clear();

#if UNITY_STEAM
            for (int i = 0; i < SteamRemoteStorage.GetFileCount(); i++)
            {
                string fileName = SteamRemoteStorage.GetFileNameAndSize(i, out _);
                if (fileName.StartsWith(_meta.FilePrefix) && LoadFromCloud(fileName, out var saveData))
                    _saves.Add(saveData);
            }
#endif

            var localSaves = _saves.Count == 0 ? _saves : new List<TSaveData>();
            var dirInfo = new DirectoryInfo(Application.persistentDataPath);
            foreach (var fileInfo in dirInfo.GetFiles())
            {
                if (!fileInfo.Name.StartsWith(_meta.FilePrefix))
                    continue;

                var saveName = Path.GetFileNameWithoutExtension(fileInfo.Name);
                if (LoadLocal(saveName, out var saveData))
                    localSaves.Add(saveData);
            }

            if (localSaves != _saves)
                _saves = MergeSaves(localSaves, _saves);

            PlayerPrefs.SetString(_meta.VersionKey, Application.version);
            PlayerPrefs.Save();
        }

        private static List<TSaveData> MergeSaves(List<TSaveData> localSaves, List<TSaveData> cloudSaves)
        {
            var allNames = localSaves.Select(s => s.Name).Union(cloudSaves.Select(s => s.Name));
            var localByName = localSaves.ToDictionary(s => s.Name);
            var cloudByName = cloudSaves.ToDictionary(s => s.Name);

            var winners = new List<TSaveData>();
            foreach (var name in allNames)
            {
                var inLocal = localByName.TryGetValue(name, out var local);
                var inCloud = cloudByName.TryGetValue(name, out var cloud);

                TSaveData winner;
                if (inLocal && inCloud)
                {
                    if (local.ContentEquals(cloud))
                    {
                        winners.Add(local);
                        continue;
                    }

                    winner = local.GetLastSaveUtc() >= cloud.GetLastSaveUtc() ? local : cloud;
                }
                else
                {
                    winner = inLocal ? local : cloud;
                }

#if UNITY_STEAM
                if (!inCloud || !winner.ContentEquals(cloud))
                    SaveToCloud(winner);
#endif
                if (!inLocal || !winner.ContentEquals(local))
                    SaveLocal(winner);

                winners.Add(winner);
            }

            var sorted = winners
                .OrderBy(s => s.Position)
                .ThenByDescending(s => s.GetLastSaveUtc())
                .ToList();

            for (int i = 0; i < sorted.Count; i++)
            {
                if (sorted[i].Position == i)
                    continue;

                var shifted = sorted[i];
                shifted.Position = i;
                sorted[i] = shifted;

#if UNITY_STEAM
                SaveToCloud(shifted);
#endif
                SaveLocal(shifted);
            }

            return sorted;
        }

        private static bool ShouldWipe()
        {
            if (!PlayerPrefs.HasKey(_meta.VersionKey))
                return false;

            return VersionUtility.CompareVersions(PlayerPrefs.GetString(_meta.VersionKey), _meta.WipeBelowVersion) < 0;
        }

        private static void Wipe()
        {
            _saves?.Clear();

            var dirInfo = new DirectoryInfo(Application.persistentDataPath);
            foreach (var fileInfo in dirInfo.GetFiles())
            {
                if (!fileInfo.Name.StartsWith(_meta.FilePrefix))
                    continue;
                fileInfo.Delete();
            }

#if UNITY_STEAM
            for (int i = SteamRemoteStorage.GetFileCount() - 1; i >= 0; i--)
            {
                string fileName = SteamRemoteStorage.GetFileNameAndSize(i, out _);
                if (fileName.StartsWith(_meta.FilePrefix))
                    DeleteFromCloud(fileName);
            }
#endif

            PlayerPrefs.DeleteKey(_meta.VersionKey);
        }

        private static string GetLocalPath(string fileName) =>
            Path.Combine(Application.persistentDataPath, fileName) + ".json";

        public static void Save(TSaveData data)
        {
            if (string.IsNullOrEmpty(_meta.FilePrefix))
                _meta = new TSaveData().Persistence;

            _saves ??= new();
            data.StampSaveTime();

            if (_saves.Count > data.Position)
                _saves[data.Position] = data;
            else
            {
                data.Position = _saves.Count;
                _saves.Add(data);
            }

            SaveLocal(data);

#if UNITY_STEAM
            SaveToCloud(data);
#endif

            PlayerPrefs.SetString(_meta.VersionKey, Application.version);
            PlayerPrefs.Save();
        }

        public static void Delete(int position)
        {
            if (position < 0 || position >= _saves.Count)
                return;
            Delete(_saves[position].Name);
        }

        public static void Delete(string fileName)
        {
            var dataIdx = _saves?.FindIndex(data => data.Name.Equals(fileName)) ?? -1;
            if (dataIdx != -1)
                _saves.RemoveAt(dataIdx);
            if (File.Exists(GetLocalPath(fileName)))
                File.Delete(GetLocalPath(fileName));

#if UNITY_STEAM
            DeleteFromCloud(fileName);
#endif
        }

        private static void SaveLocal(TSaveData data) => SaveLocal(data.Name, data.ToJson());

        private static void SaveLocal(string fileName, string json)
        {
            try
            {
                File.WriteAllText(GetLocalPath(fileName), json, Encoding.UTF8);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Local save exception: {e.Message}");
            }
        }

        private static bool LoadLocal(string fileName, out TSaveData data)
        {
            data = default;
            if (!File.Exists(GetLocalPath(fileName)))
            {
                Debug.Log($"[SaveManager] Local file not found: {GetLocalPath(fileName)}");
                return false;
            }

            try
            {
                string json = File.ReadAllText(GetLocalPath(fileName), Encoding.UTF8);
                data = Serializer.FromJson(json);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Local file read exception: {e.Message}");
                return false;
            }
        }

#if UNITY_STEAM
        private static void SaveToCloud(TSaveData data) =>
            SaveToCloud(data.Name, Encoding.UTF8.GetBytes(data.ToJson()));

        private static void SaveToCloud(string fileName, byte[] bytes)
        {
            if (!Steamworks.SteamRemoteStorage.IsCloudEnabledForAccount() ||
                !Steamworks.SteamRemoteStorage.IsCloudEnabledForApp())
            {
                Debug.LogWarning("[SaveManager] Steam Cloud disabled.");
                return;
            }

            if (!Steamworks.SteamRemoteStorage.FileWrite(fileName, bytes, bytes.Length))
                Debug.LogError("[SaveManager] Steam Cloud save failed.");
        }

        private static bool LoadFromCloud(string fileName, out TSaveData data)
        {
            data = default;
            if (!Steamworks.SteamRemoteStorage.FileExists(fileName))
            {
                Debug.Log("[SaveManager] Steam Cloud file not found.");
                return false;
            }

            try
            {
                int size = Steamworks.SteamRemoteStorage.GetFileSize(fileName);
                byte[] bytes = new byte[size];
                int read = Steamworks.SteamRemoteStorage.FileRead(fileName, bytes, size);

                if (read == 0)
                {
                    Debug.LogWarning("[SaveManager] Steam Cloud returned empty file.");
                    return false;
                }

                string json = Encoding.UTF8.GetString(bytes, 0, read);
                data = Serializer.FromJson(json);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Steam Cloud read error: {e.Message}");
                return false;
            }
        }

        private static void DeleteFromCloud(string fileName)
        {
            if (!Steamworks.SteamRemoteStorage.FileExists(fileName))
                return;
            Steamworks.SteamRemoteStorage.FileDelete(fileName);
        }
#endif
    }
}
