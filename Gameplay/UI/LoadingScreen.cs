using System;
using System.Reflection;
using System.Text;
using CodexFramework.Utils;
using UnityEngine;
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
        [Header("Loading status")]
        [SerializeField]
        private MonoBehaviour _loadingText;
        [SerializeField, Min(0.05f)]
        private float _loadingAnimationDelay = 0.5f;
        [SerializeField]
        private Image _progressBar;
        [SerializeField]
        private MonoBehaviour _progressText;
        [Header("Backgrounds")]
        [SerializeField]
        private Image _backgroundImage;
        [SerializeField, Min(0.05f)]
        private float _loadingChangeBGDelay = 3.0f;
        [SerializeField]
        private Sprite[] _bgSprites = Array.Empty<Sprite>();
        [Header("Tooltips")]
        [SerializeField]
        private MonoBehaviour _tooltipText;
        [SerializeField, TextArea(2, 4)]
        private string[] _tooltipTexts = Array.Empty<string>();
        [SerializeField, Min(0.05f)]
        private float _tooltipChangeDelay = 5.0f;

        private Image _bg;

        private string _initialLoadingString;
        private StringBuilder _loadingStringBuilder;
        private const int MaxDotsCount = 3;
        private int _currentDotsCount;
        private float _loadingAnimationCD;
        private float _loadingChangeBGCD;
        private float _tooltipChangeCD;
        private int _currentBgSpriteIdx = -1;
        private int _currentTooltipIdx = -1;
        private ILoadingScreenText _loadingTextAccessor;
        private ILoadingScreenText _tooltipTextAccessor;
        private ILoadingScreenText _progressTextAccessor;
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
            _bg = _backgroundImage != null ? _backgroundImage : GetComponent<Image>();
            _loadingTextAccessor = _loadingText != null ? ResolveLoadingText(_loadingText) : null;
            _tooltipTextAccessor = _tooltipText != null ? ResolveLoadingText(_tooltipText) : null;
            _progressTextAccessor = _progressText != null ? ResolveLoadingText(_progressText) : null;
            _initialLoadingString = _loadingTextAccessor?.Text ?? "Loading";
            _loadingStringBuilder = new StringBuilder();
        }

        private void OnEnable() => BeginLoading();

        public void Show()
        {
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
            _failed = false;
            _currentDotsCount = 0;
            if (_loadingTextAccessor != null)
                _loadingTextAccessor.Text = _initialLoadingString;
            Progress = 0f;
            SetProgress(0f);
            _loadingAnimationCD = _loadingAnimationDelay;
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
            Progress = Mathf.Max(Progress, Mathf.Clamp01(value));
            if (_progressBar != null)
            {
                // Stretch the fill inside its track; a sprite-less Image works too.
                var fill = _progressBar.rectTransform;
                fill.anchorMax = new Vector2(Progress, fill.anchorMax.y);
            }
            if (_progressTextAccessor != null)
                _progressTextAccessor.Text = $"{Mathf.FloorToInt(Progress * 100f)}%";
        }

        public void ShowFailure(string message)
        {
            EnsureInitialized();
            _failed = true;
            if (_loadingTextAccessor != null)
                _loadingTextAccessor.Text = message;
        }

        void Update()
        {
            _loadingAnimationCD -= Time.unscaledDeltaTime;
            _loadingChangeBGCD -= Time.unscaledDeltaTime;
            _tooltipChangeCD -= Time.unscaledDeltaTime;

            if (!_failed && _loadingAnimationCD <= 0)
            {
                AnimateText();
                _loadingAnimationCD = Mathf.Max(0.05f, _loadingAnimationDelay);
            }

            if (_loadingChangeBGCD <= 0)
            {
                ChangeBackground();
                _loadingChangeBGCD = Mathf.Max(0.05f, _loadingChangeBGDelay);
            }
            if (_tooltipChangeCD <= 0)
            {
                ChangeTooltip();
                _tooltipChangeCD = Mathf.Max(0.05f, _tooltipChangeDelay);
            }
        }

        private void ChangeBackground()
        {
            if (_bg == null)
                return;
            _currentBgSpriteIdx = NextIndex(_bgSprites, _currentBgSpriteIdx, sprite => sprite != null);
            if (_currentBgSpriteIdx >= 0)
                _bg.sprite = _bgSprites[_currentBgSpriteIdx];
        }

        private void ChangeTooltip()
        {
            if (_tooltipTextAccessor == null)
                return;
            _currentTooltipIdx = NextIndex(_tooltipTexts, _currentTooltipIdx,
                text => !string.IsNullOrWhiteSpace(text));
            _tooltipTextAccessor.Text = _currentTooltipIdx >= 0 ? _tooltipTexts[_currentTooltipIdx] : string.Empty;
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

        private void AnimateText()
        {
            if (_loadingTextAccessor == null)
                return;
            _loadingStringBuilder.Clear();
            _loadingStringBuilder.Append(_initialLoadingString);
            _currentDotsCount++;
            _currentDotsCount %= MaxDotsCount + 1;
            for (int i = 0; i < _currentDotsCount; i++)
                _loadingStringBuilder.Append('.');
            _loadingTextAccessor.Text = _loadingStringBuilder.ToString();
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