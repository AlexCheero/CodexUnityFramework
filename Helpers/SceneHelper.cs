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
        public static event Action<string> OnSceneLoaded;
        public static bool IsOnSceneLoadedAssigned => OnSceneLoaded != null;

        private const float _minLoadTime = 0.0f;

        public static void ResetScene() => LoadScene(SceneManager.GetActiveScene().name);

        public static void LoadScene(string name)
        {
            CoroutineRunner.Instance.StartCoroutine(LoadSceneRoutine(name, _minLoadTime));

            //AdsManager.Instance.ShowInter(() =>
            //{
            //    CoroutineRunner.Instance.StartCoroutine(LoadSceneRoutine(name, _minLoadTime));
            //});
        }

        private static IEnumerator LoadSceneRoutine(string levelName, float minLoadTime)
        {
            if (ECSPipelineController.IsCreated)
                ECSPipelineController.Instance.Pause();
            if (LoadingScreen.IsCreated)
                LoadingScreen.Instance.gameObject.SetActive(true);

            var asyncOp = SceneManager.LoadSceneAsync(levelName);
            asyncOp.allowSceneActivation = false;

            while (minLoadTime > 0)
            {
                minLoadTime -= Time.deltaTime;
                yield return null;
            }

            asyncOp.allowSceneActivation = true;
            asyncOp.completed += _ => OnSceneLoaded?.Invoke(levelName);
        }
    }
}