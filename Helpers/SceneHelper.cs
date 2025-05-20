using System;
using CodexFramework.CodexEcsUnityIntegration;
using CodexFramework.Gameplay.UI;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CodexFramework.Helpers
{
    public static class SceneHelper
    {
        private const float _minLoadTime = 0.0f;

        public static void ResetScene() => LoadScene(SceneManager.GetActiveScene().name);

        private static bool _loadStarted;
        public static void LoadScene(string name, LoadSceneMode loadMode = LoadSceneMode.Single, Action<string> onLoadComplete = null)
        {
            if (_loadStarted)
                return;
            
            _loadStarted = true;
            CoroutineRunner.Instance.StartCoroutine(LoadSceneRoutine(name, _minLoadTime, loadMode, onLoadComplete));

            //AdsManager.Instance.ShowInter(() =>
            //{
            //    CoroutineRunner.Instance.StartCoroutine(LoadSceneRoutine(name, _minLoadTime));
            //});
        }

        private static IEnumerator LoadSceneRoutine(string levelName, float minLoadTime,
            LoadSceneMode loadMode = LoadSceneMode.Single, Action<string> onLoadComplete = null)
        {
            if (ECSPipelineController.IsCreated)
                ECSPipelineController.Instance.Pause();
            if (LoadingScreen.IsCreated)
                LoadingScreen.Instance.gameObject.SetActive(true);

            var asyncOp = SceneManager.LoadSceneAsync(levelName, loadMode);
            asyncOp.allowSceneActivation = false;

            while (minLoadTime > 0)
            {
                minLoadTime -= Time.deltaTime;
                yield return null;
            }

            asyncOp.allowSceneActivation = true;
            asyncOp.completed += _ =>
            {
                _loadStarted = false;
                onLoadComplete?.Invoke(levelName);
                Time.timeScale = 1.0f;//restore time scale on load new scene
            };
        }
    }
}