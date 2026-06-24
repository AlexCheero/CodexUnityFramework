using System;
using System.Collections;
using System.Collections.Generic;
using CodexFramework.CodexEcsUnityIntegration;
using CodexFramework.Gameplay.UI;
using CodexFramework.Helpers;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CodexFramework.Scenes
{
    [Serializable]
    public struct SceneEntry
    {
        public string ScenePath;
    }

    public sealed class SceneEntryComparer : IEqualityComparer<SceneEntry>
    {
        public static readonly SceneEntryComparer Instance = new();

        public bool Equals(SceneEntry x, SceneEntry y) => x.ScenePath == y.ScenePath;

        public int GetHashCode(SceneEntry obj) =>
            obj.ScenePath != null ? obj.ScenePath.GetHashCode() : 0;
    }
    
    public static partial class SceneHelper
    {
        private const float _minLoadTime = 0.0f;

        public static void ResetScene() => LoadScene(SceneManager.GetActiveScene().name);

        private static bool _loadStarted;

        public static void LoadScene(SceneEntry scene, LoadSceneMode loadMode = LoadSceneMode.Single,
            Action<string> onLoadComplete = null)
        {
            LoadScene(scene.ScenePath, loadMode, onLoadComplete);
        }
        
        public static void LoadScene(string name, LoadSceneMode loadMode = LoadSceneMode.Single, Action<string> onLoadComplete = null)
        {
            if (_loadStarted)
                return;
            
            _loadStarted = true;
            
            CoroutineRunner.Instance.StartCoroutine(LoadSceneRoutine(name, _minLoadTime, loadMode, onLoadComplete));
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
                Time.timeScale = 1.0f;
            };
        }

        public static bool IsSceneLoaded(string name) => SceneManager.GetSceneByName(name).isLoaded;
    }
}
