using CodexFramework.Utils;

namespace CodexFramework.Helpers
{
    public class CoroutineRunner : Singleton<CoroutineRunner>
    {
        protected override void Init()
        {
            base.Init();
            DontDestroyOnLoad(gameObject);
        }
    }
}