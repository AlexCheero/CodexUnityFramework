namespace CodexFramework.Bootstrap
{
    public abstract class GameRegistryBase<TSelf> where TSelf : GameRegistryBase<TSelf>, new()
    {
        private static TSelf _instance;
        public static TSelf Instance => _instance ??= new TSelf();

        public static bool IsCreated => _instance != null;

        public static void Initialize() => Instance.OnInitialize();

        protected abstract void OnInitialize();
    }
}
