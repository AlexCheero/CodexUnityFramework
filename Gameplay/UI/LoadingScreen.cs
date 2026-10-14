using System;
using System.IO;
using System.Reflection;
using CodexFramework.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CodexFramework.Gameplay.UI
{
    /// <summary>
    /// Package-neutral text contract used by <see cref="LoadingScreen"/>.
    /// Custom text adapters can implement this instead of exposing a public
    /// <c>text</c> property.
    /// </summary>
    public interface ILoadingScreenText
    {
        string Text { get; set; }
    }

    public class LoadingScreen : Singleton<LoadingScreen>
    {
        [Serializable]
        private sealed class SceneBackgrounds
        {
            public string SceneName;
            public Sprite[] Sprites;
        }

        [Header("Loading progress and failure")]
        [SerializeField]
        private MonoBehaviour _failureText;
        [SerializeField]
        private Image _progressBar;
        [Header("Backgrounds")]
        [SerializeField]
        private Image _backgroundImage;
        [SerializeField, Min(0.05f)]
        private float _loadingChangeBGDelay = 3.0f;
        [SerializeField]
        private SceneBackgrounds[] _sceneBackgrounds;
        [Header("Tooltips")]
        [SerializeField]
        private MonoBehaviour _tooltipText;
        [SerializeField, TextArea(2, 4)]
        private string[] _tooltipTexts = Array.Empty<string>();
        [SerializeField, Min(0.05f)]
        private float _tooltipChangeDelay = 5.0f;

        private Sprite[] _bgSprites;
        private string _destinationScene;

        private float _loadingChangeBGCD;
        private float _tooltipChangeCD;
        private int _currentBgSpriteIdx = -1;
        private int _currentTooltipIdx = -1;
        private ILoadingScreenText _failureTextAccessor;
        private ILoadingScreenText _tooltipTextAccessor;
        private readonly System.Random _random = new System.Random();
        private bool _initialized;
        private bool _failed;

        public float Progress { get; private set; }

        protected override void Init()
        {
            base.Init();
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (_initialized)
                return;
            _initialized = true;
            _failureTextAccessor = ResolveLoadingText(_failureText);
            _tooltipTextAccessor = ResolveLoadingText(_tooltipText);
        }

        private void OnEnable() => BeginLoading();

        public void Show(string destinationScene)
        {
            _destinationScene = Path.GetFileNameWithoutExtension(destinationScene);
            transform.SetAsLastSibling();
            if (gameObject.activeSelf)
                BeginLoading();
            else
                gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        private void BeginLoading()
        {
            EnsureInitialized();
            var sceneName = _destinationScene ?? SceneManager.GetActiveScene().name;
            var backgrounds = _sceneBackgrounds == null ? null : Array.Find(_sceneBackgrounds,
                entry => entry != null && entry.SceneName == sceneName);
            _bgSprites = backgrounds?.Sprites ?? Array.Empty<Sprite>();
            _currentBgSpriteIdx = -1;
            _failed = false;
            _failureText.gameObject.SetActive(false);
            Progress = 0f;
            SetProgress(0f);
            _loadingChangeBGCD = _loadingChangeBGDelay;
            _tooltipChangeCD = _tooltipChangeDelay;
            ChangeBackground();
            ChangeTooltip();
        }

        public void SetProgress(float value)
        {
            EnsureInitialized();
            if (float.IsNaN(value))
                return;
            var progress = Mathf.Max(Progress, Mathf.Clamp01(value));
            if (_progressBar.fillAmount != progress) _progressBar.fillAmount = progress;
            Progress = progress;
        }

        public void ShowFailure(string message)
        {
            EnsureInitialized();
            _failed = true;
            _failureText.gameObject.SetActive(true);
            _failureTextAccessor.Text = message;
            _tooltipText.gameObject.SetActive(false);
        }

        void Update()
        {
            _loadingChangeBGCD -= Time.unscaledDeltaTime;
            _tooltipChangeCD -= Time.unscaledDeltaTime;

            if (_loadingChangeBGCD <= 0)
            {
                ChangeBackground();
                _loadingChangeBGCD = Mathf.Max(0.05f, _loadingChangeBGDelay);
            }
            if (!_failed && _tooltipChangeCD <= 0)
            {
                ChangeTooltip();
                _tooltipChangeCD = Mathf.Max(0.05f, _tooltipChangeDelay);
            }
        }

        private void ChangeBackground()
        {
            var next = NextIndex(_bgSprites, _currentBgSpriteIdx, sprite => sprite != null);
            if (next < 0)
            {
                _backgroundImage.sprite = null;
                _backgroundImage.color = Color.black;
                return;
            }
            if (next == _currentBgSpriteIdx) return;
            _currentBgSpriteIdx = next;
            _backgroundImage.sprite = _bgSprites[next];
            _backgroundImage.color = Color.white;
        }

        private void ChangeTooltip()
        {
            _tooltipText.gameObject.SetActive(true);
            _currentTooltipIdx = NextIndex(_tooltipTexts, _currentTooltipIdx,
                text => !string.IsNullOrWhiteSpace(text));
            var text = _currentTooltipIdx >= 0 ? _tooltipTexts[_currentTooltipIdx] : string.Empty;
            if (_tooltipTextAccessor.Text != text) _tooltipTextAccessor.Text = text;
        }

        private int NextIndex<T>(T[] entries, int current, Func<T, bool> isValid)
        {
            if (entries == null || entries.Length == 0)
                return -1;
            var selected = -1;
            var candidates = 0;
            for (var i = 0; i < entries.Length; i++)
            {
                if (i == current || !isValid(entries[i]))
                    continue;
                if (_random.Next(++candidates) == 0)
                    selected = i;
            }
            return selected >= 0 ? selected :
                current >= 0 && current < entries.Length && isValid(entries[current]) ? current : -1;
        }

        private static ILoadingScreenText ResolveLoadingText(MonoBehaviour component)
        {
            if (component == null)
                throw new InvalidOperationException($"{nameof(LoadingScreen)} requires a loading text component.");

            if (component is ILoadingScreenText loadingText)
                return loadingText;

            // Compatibility bridge for existing text components (including TMP and
            // legacy UGUI Text) without taking a compile-time dependency on either.
            var property = component.GetType().GetProperty(
                "text",
                BindingFlags.Instance | BindingFlags.Public);
            if (property == null || property.PropertyType != typeof(string) ||
                !property.CanRead || !property.CanWrite)
            {
                throw new InvalidOperationException(
                    $"{component.GetType().FullName} must implement {nameof(ILoadingScreenText)} " +
                    "or expose a public readable/writable string text property.");
            }

            return new ReflectedLoadingScreenText(component, property);
        }

        private sealed class ReflectedLoadingScreenText : ILoadingScreenText
        {
            private readonly MonoBehaviour _component;
            private readonly PropertyInfo _property;

            public ReflectedLoadingScreenText(MonoBehaviour component, PropertyInfo property)
            {
                _component = component;
                _property = property;
            }

            public string Text
            {
                get => (string)_property.GetValue(_component);
                set => _property.SetValue(_component, value);
            }
        }
    }
}
