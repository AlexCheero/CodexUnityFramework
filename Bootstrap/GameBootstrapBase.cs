using CodexFramework.Saves;
using CodexFramework.Settings;

namespace CodexFramework.Bootstrap
{
    public abstract class GameBootstrapBase<TSelf, TRegistry, TSettings, TSettingsSerializer, TSaveData, TSaveSerializer>
        where TSelf : GameBootstrapBase<TSelf, TRegistry, TSettings, TSettingsSerializer, TSaveData, TSaveSerializer>, new()
        where TRegistry : GameRegistryBase<TRegistry>, new()
        where TSettings : IGameSettings, new()
        where TSettingsSerializer : ISettingsSerializer, new()
        where TSaveData : IGameSaveData<TSaveData>, new()
        where TSaveSerializer : ISaveDataSerializer<TSaveData>, new()
    {
        protected static void Initialize()
        {
            GameRegistryBase<TRegistry>.Initialize();
            SettingsManagerBase<TSettings, TSettingsSerializer>.Load();
            SaveManagerBase<TSaveData, TSaveSerializer>.LoadSaves();
            new TSelf().OnBootstrap();
        }

        protected virtual void OnBootstrap() { }
    }
}
