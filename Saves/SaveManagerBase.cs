using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CodexFramework.Utils;
using UnityEngine;

namespace CodexFramework.Saves
{
    public abstract class SaveManagerBase<TSaveData, TSerializer>
        where TSaveData : IGameSaveData<TSaveData>, new()
        where TSerializer : ISaveDataSerializer<TSaveData>, new()
    {
        private static readonly TSerializer Serializer = new();
        private static List<TSaveData> _saves;
        private static SavePersistenceMeta _meta;
        private static ISaveCloudStorage _cloud;
        private static string _directory;
        public static IReadOnlyList<TSaveData> Saves => _saves;
        private static string DirectoryPath => _directory ?? Application.persistentDataPath;
        private static bool SyncCloud => _cloud != null && _cloud.Enabled;

        // Null cloud explicitly selects local storage (non-Steam builds and edit-mode tools).
        public static void ConfigureStorage(string directory, ISaveCloudStorage cloud)
        {
            _directory = directory;
            _cloud = cloud;
            _saves = null;
            _meta = new TSaveData().Persistence;
        }

        public static void LoadSaves()
        {
            _meta = new TSaveData().Persistence;
            _saves = null;
            Directory.CreateDirectory(DirectoryPath);
            if (ShouldWipe()) Wipe();
            var local = new List<TSaveData>();
            foreach (var path in Directory.GetFiles(DirectoryPath).Where(path => IsSaveFile(Path.GetFileName(path))))
                local.Add(ReadSave(Path.GetFileName(path), File.ReadAllText(path, Encoding.UTF8)));
            var cloud = new List<TSaveData>();
            bool syncCloud = SyncCloud;
            if (syncCloud)
                foreach (var fileName in _cloud.GetFileNames().Where(IsSaveFile))
                    cloud.Add(ReadSave(fileName, Encoding.UTF8.GetString(_cloud.Read(fileName))));
            // Reconcile even an empty cloud on the first Steam launch.
            _saves = MergeSaves(local, cloud, syncCloud);
            RememberVersion();
        }

        private static bool IsSaveFile(string fileName) =>
            fileName.StartsWith(_meta.FilePrefix, StringComparison.Ordinal) &&
            fileName.EndsWith(".json", StringComparison.Ordinal);

        private static TSaveData ReadSave(string fileName, string json)
        {
            // Read/format failures stop loading, never create and upload a replacement profile.
            var data = Serializer.FromJson(json);
            if (GetFileName(data.Name) != fileName)
                throw new InvalidDataException($"Save '{fileName}' contains a different profile name '{data.Name}'.");
            data.GetLastSaveUtc();
            return data;
        }

        private static List<TSaveData> MergeSaves(List<TSaveData> localSaves, List<TSaveData> cloudSaves, bool syncCloud)
        {
            var local = localSaves.ToDictionary(save => save.Name);
            var cloud = cloudSaves.ToDictionary(save => save.Name);
            var winners = new List<TSaveData>();
            foreach (var name in local.Keys.Union(cloud.Keys))
            {
                bool inLocal = local.TryGetValue(name, out var localSave);
                bool inCloud = cloud.TryGetValue(name, out var cloudSave);
                var winner = !inLocal ? cloudSave : !inCloud ? localSave :
                    localSave.GetLastSaveUtc() >= cloudSave.GetLastSaveUtc() ? localSave : cloudSave;
                winners.Add(winner);
            }
            var sorted = winners.OrderBy(save => save.Position).ThenByDescending(save => save.GetLastSaveUtc()).ToList();
            for (int i = 0; i < sorted.Count; i++)
            {
                var save = sorted[i];
                bool shifted = save.Position != i;
                save.Position = i;
                sorted[i] = save;
                // Preserve the winning timestamp even when gameplay contents are equal.
                string json = save.ToJson();
                if (shifted || !local.TryGetValue(save.Name, out var localSave) || localSave.ToJson() != json)
                    SaveLocal(save.Name, json);
                if (syncCloud && (shifted || !cloud.TryGetValue(save.Name, out var cloudSave) || cloudSave.ToJson() != json))
                    _cloud.Write(GetFileName(save.Name), Encoding.UTF8.GetBytes(json));
            }
            return sorted;
        }

        private static bool ShouldWipe() => PlayerPrefs.HasKey(_meta.VersionKey) &&
            VersionUtility.CompareVersions(PlayerPrefs.GetString(_meta.VersionKey), _meta.WipeBelowVersion) < 0;

        private static void Wipe()
        {
            _saves?.Clear();
            if (SyncCloud)
                foreach (var fileName in _cloud.GetFileNames().Where(IsSaveFile).ToArray())
                    _cloud.Delete(fileName);
            foreach (var path in Directory.GetFiles(DirectoryPath).Where(path => IsSaveFile(Path.GetFileName(path))))
                File.Delete(path);
            PlayerPrefs.DeleteKey(_meta.VersionKey);
        }

        private static string GetFileName(string name) => name + ".json";
        private static string GetLocalPath(string name) => Path.Combine(DirectoryPath, GetFileName(name));

        public static void Save(TSaveData data)
        {
            _meta = new TSaveData().Persistence;
            _saves ??= new();
            data.StampSaveTime();
            int index = _saves.FindIndex(save => save.Name == data.Name);
            data.Position = index < 0 ? _saves.Count : index;
            if (index < 0) _saves.Add(data);
            else _saves[index] = data;
            string json = data.ToJson();
            SaveLocal(data.Name, json);
            if (SyncCloud) _cloud.Write(GetFileName(data.Name), Encoding.UTF8.GetBytes(json));
            RememberVersion();
        }

        public static void Delete(int position)
        {
            if (_saves == null || position < 0 || position >= _saves.Count) return;
            Delete(_saves[position].Name);
        }

        public static void Delete(string name)
        {
            if (SyncCloud) _cloud.Delete(GetFileName(name));
            File.Delete(GetLocalPath(name));
            int index = _saves?.FindIndex(save => save.Name == name) ?? -1;
            if (index < 0) return;
            _saves.RemoveAt(index);
            for (int i = index; i < _saves.Count; i++)
            {
                var save = _saves[i];
                save.Position = i;
                _saves[i] = save;
                string json = save.ToJson();
                SaveLocal(save.Name, json);
                if (SyncCloud) _cloud.Write(GetFileName(save.Name), Encoding.UTF8.GetBytes(json));
            }
        }

        private static void SaveLocal(string name, string json)
        {
            Directory.CreateDirectory(DirectoryPath);
            string path = GetLocalPath(name);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, json, new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }

        private static void RememberVersion()
        {
            PlayerPrefs.SetString(_meta.VersionKey, Application.version);
            PlayerPrefs.Save();
        }
    }
}
