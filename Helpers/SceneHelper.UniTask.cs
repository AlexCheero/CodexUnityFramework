// UniTask has no global define; enable overloads by adding UNITASK_SUPPORT to Scripting Define Symbols
// or via an asmdef versionDefine on com.cysharp.unitask.
#if UNITASK_SUPPORT
using System;
using System.Threading;
using CodexFramework.CodexEcsUnityIntegration;
using CodexFramework.Gameplay.UI;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CodexFramework.Scenes
{
    public static partial class SceneHelper
    {
        public static UniTask LoadSceneAsync(string name, LoadSceneMode loadMode = LoadSceneMode.Single,
            CancellationToken cancellationToken = default)
        {
            if (_loadStarted)
                return UniTask.CompletedTask;

            _loadStarted = true;

            OnSceneLoadStarted(name);
            SceneLoadStarted?.Invoke(name);

            return LoadSceneAsyncCore(name, _minLoadTime, loadMode, cancellationToken);
        }

        private static async UniTask LoadSceneAsyncCore(string levelName, float minLoadTime, LoadSceneMode loadMode,
            CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (ECSPipelineController.IsCreated)
                    ECSPipelineController.Instance.Pause();
                if (LoadingScreen.IsCreated)
                    LoadingScreen.Instance.gameObject.SetActive(true);

                var asyncOp = SceneManager.LoadSceneAsync(levelName, loadMode);
                asyncOp.allowSceneActivation = false;

                while (minLoadTime > 0)
                {
                    minLoadTime -= Time.deltaTime;
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }

                asyncOp.allowSceneActivation = true;
                await asyncOp.ToUniTask(cancellationToken: cancellationToken);

                OnSceneLoadCompleted(levelName);
                SceneLoadCompleted?.Invoke(levelName);

                Time.timeScale = 1.0f;
            }
            finally
            {
                _loadStarted = false;
            }
        }
    }
}
#endif
