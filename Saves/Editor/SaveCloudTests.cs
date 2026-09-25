#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace CodexFramework.Saves.Tests
{
    public sealed class SaveCloudTests
    {
        private sealed class Manager : SaveManagerBase<TestSave, Serializer> { }
        [Serializable]
        public sealed class TestSave : IGameSaveData<TestSave>
        {
            public string name = "test_player";
            public int position;
            public string timestamp;
            public string value;
            public SavePersistenceMeta Persistence => new() { FilePrefix = "test_", VersionKey = "SteamIntegrationTests", WipeBelowVersion = "0.0.0" };
            public string Name => name;
            public int Position { get => position; set => position = value; }
            public string LastSaveDateTime { get => timestamp; set => timestamp = value; }
            public string ToJson() => JsonUtility.ToJson(this);
            public bool ContentEquals(TestSave other) => value == other.value;
            public DateTime GetLastSaveUtc() => DateTime.Parse(timestamp, null, System.Globalization.DateTimeStyles.RoundtripKind);
            public void StampSaveTime() => timestamp = DateTime.UtcNow.ToString("o");
        }
        public sealed class Serializer : ISaveDataSerializer<TestSave>
        {
            public TestSave FromJson(string json) => JsonUtility.FromJson<TestSave>(json);
        }
        private sealed class Cloud : ISaveCloudStorage
        {
            public bool Enabled { get; set; } = true;
            public readonly Dictionary<string, byte[]> Files = new();
            public int Writes;
            public bool FailRead;
            public IEnumerable<string> GetFileNames() => Files.Keys.ToArray();
            public byte[] Read(string name) => FailRead ? throw new IOException("Short cloud read") : Files[name];
            public void Write(string name, byte[] bytes) { Files[name] = bytes; Writes++; }
            public void Delete(string name) => Files.Remove(name);
        }
        private string directory;
        private Cloud cloud;
        private static TestSave SaveAt(int day, string value = "progress", string name = "test_player") =>
            new() { name = name, timestamp = new DateTime(2026, 9, day, 0, 0, 0, DateTimeKind.Utc).ToString("o"), value = value };
        private void Local(TestSave save) => File.WriteAllText(Path.Combine(directory, save.Name + ".json"), save.ToJson());
        private void Remote(TestSave save) => cloud.Files[save.Name + ".json"] = Encoding.UTF8.GetBytes(save.ToJson());
        private TestSave ReadLocal() => JsonUtility.FromJson<TestSave>(File.ReadAllText(Path.Combine(directory, "test_player.json")));
        private TestSave ReadRemote() => JsonUtility.FromJson<TestSave>(Encoding.UTF8.GetString(cloud.Files["test_player.json"]));

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "SteamSaveTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            cloud = new Cloud();
            PlayerPrefs.DeleteKey("SteamIntegrationTests");
            Manager.ConfigureStorage(directory, cloud);
        }
        [TearDown]
        public void TearDown()
        {
            Manager.ConfigureStorage(null, null);
            Directory.Delete(directory, true);
            PlayerPrefs.DeleteKey("SteamIntegrationTests");
        }

        [Test] public void EmptyCloudReceivesExistingLocalProgress()
        {
            Local(SaveAt(1));
            Manager.LoadSaves();
            Assert.That(ReadRemote().value, Is.EqualTo("progress"));
            Assert.That(cloud.Writes, Is.EqualTo(1));
        }
        [Test] public void CloudOnlyProfileIsCachedLocally()
        {
            Remote(SaveAt(2));
            Manager.LoadSaves();
            Assert.That(ReadLocal().timestamp, Is.EqualTo(SaveAt(2).timestamp));
        }
        [TestCase(true)] [TestCase(false)]
        public void NewestProgressWinsOnBothStores(bool cloudNewer)
        {
            Local(SaveAt(cloudNewer ? 1 : 2, cloudNewer ? "old" : "new"));
            Remote(SaveAt(cloudNewer ? 2 : 1, cloudNewer ? "new" : "old"));
            Manager.LoadSaves();
            Assert.That(ReadLocal().value, Is.EqualTo("new"));
            Assert.That(ReadRemote().value, Is.EqualTo("new"));
        }
        [Test] public void EqualContentsStillSynchronizeNewestTimestamp()
        {
            Local(SaveAt(1)); Remote(SaveAt(2));
            Manager.LoadSaves();
            Assert.That(ReadLocal().timestamp, Is.EqualTo(ReadRemote().timestamp));
        }
        [Test] public void DisabledCloudIsNeverReadOrWritten()
        {
            Local(SaveAt(1)); Remote(SaveAt(2, "remote"));
            cloud.Enabled = false; cloud.FailRead = true;
            Manager.LoadSaves();
            Assert.That(Manager.Saves.Single().value, Is.EqualTo("progress"));
            Assert.That(cloud.Writes, Is.Zero);
        }
        [Test] public void FailedCloudReadDoesNotOverwriteEitherCopy()
        {
            Local(SaveAt(1)); Remote(SaveAt(2)); cloud.FailRead = true;
            Assert.Throws<IOException>(() => Manager.LoadSaves());
            Assert.That(ReadLocal().timestamp, Is.EqualTo(SaveAt(1).timestamp));
            Assert.That(cloud.Writes, Is.Zero);
            Assert.That(Manager.Saves, Is.Null);
        }
        [Test] public void InvalidLocalTimestampCannotOverwriteCloud()
        {
            var invalid = SaveAt(1); invalid.timestamp = "invalid";
            Local(invalid); Remote(SaveAt(2));
            Assert.Throws<FormatException>(() => Manager.LoadSaves());
            Assert.That(cloud.Writes, Is.Zero);
        }
        [Test] public void InvalidCloudTimestampCannotOverwriteLocal()
        {
            var invalid = SaveAt(2); invalid.timestamp = "invalid";
            Local(SaveAt(1)); Remote(invalid);
            Assert.Throws<FormatException>(() => Manager.LoadSaves());
            Assert.That(ReadLocal().timestamp, Is.EqualTo(SaveAt(1).timestamp));
        }
        [Test] public void SaveAndDeleteUseTheSameJsonFileName()
        {
            Manager.LoadSaves();
            Manager.Save(SaveAt(1, "Прогресс 日本語"));
            Assert.That(ReadRemote().value, Is.EqualTo(ReadLocal().value));
            Manager.Delete("test_player");
            Assert.That(cloud.Files, Is.Empty);
            Assert.That(Directory.GetFiles(directory), Is.Empty);
        }
        [Test] public void TemporaryFilesAreNotLoadedAsProfiles()
        {
            Local(SaveAt(1));
            File.WriteAllText(Path.Combine(directory, "test_player.json.tmp"), "broken");
            Manager.LoadSaves();
            Assert.That(Manager.Saves.Count, Is.EqualTo(1));
        }
        [Test] public void SavingDifferentNamesDoesNotReplaceAnExistingPosition()
        {
            Manager.LoadSaves();
            Manager.Save(SaveAt(1)); Manager.Save(SaveAt(1, name: "test_other"));
            Assert.That(Manager.Saves.Count, Is.EqualTo(2));
            Manager.Delete("test_player");
            Assert.That(Manager.Saves.Single().Name, Is.EqualTo("test_other"));
            Assert.That(Manager.Saves.Single().Position, Is.Zero);
        }
        [Test] public void EmbeddedProfileNameMustMatchFileName()
        {
            cloud.Files["test_player.json"] = Encoding.UTF8.GetBytes(SaveAt(1, name: "test_other").ToJson());
            Assert.Throws<InvalidDataException>(() => Manager.LoadSaves());
            Assert.That(Directory.GetFiles(directory), Is.Empty);
        }
    }
}
#endif
