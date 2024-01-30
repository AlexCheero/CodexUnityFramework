using System;
using CodexFramework.Mangers;
using CodexFramework.Utils;

namespace CodexFramework.Templates
{
    public class AdsManager : Singleton<AdsManager>
    {
        protected override void Init()
        {
            base.Init();
        }

        private Action _onInterClosedCallback;
        public void ShowInter(Action onInterClosedCallback, bool ignoreDelay = false)
        {
            if (AudioManager.IsCreated)
                AudioManager.Instance.Mute();
            _onInterClosedCallback = onInterClosedCallback;
        }

        private Action _onRewardedClosedCallback;
        public void ShowRewarded(Action onRewardedClosedCallback)
        {
            if (AudioManager.IsCreated)
                AudioManager.Instance.Mute();
            _onRewardedClosedCallback = onRewardedClosedCallback;
        }
    }
}