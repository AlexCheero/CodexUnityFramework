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
        [SerializeField]
        private MonoBehaviour _loadingText;
        [SerializeField]
        private float _loadingAnimationDelay = 0.5f;
        [SerializeField]
        private float _loadingChangeBGDelay = 3.0f;
        [SerializeField]
        private Sprite[] _bgSprites;

        private Image _bg;

        private string _initialLoadingString;
        private StringBuilder _loadingStringBuilder;
        private const int MaxDotsCount = 3;
        private int _currentDotsCount;
        private float _loadingAnimationCD;
        private float _loadingChangeBGCD;
        private int _currentBgSpriteIdx;
        private ILoadingScreenText _loadingTextAccessor;

        void Awake()
        {
            _bg = GetComponent<Image>();
            _bg.sprite = _bgSprites[0];

            _loadingTextAccessor = ResolveLoadingText(_loadingText);
            _initialLoadingString = _loadingTextAccessor.Text;
            _loadingStringBuilder = new StringBuilder();

            _loadingAnimationCD = _loadingAnimationDelay;
            _loadingChangeBGCD = _loadingChangeBGDelay;
        }

        void Update()
        {
            _loadingAnimationCD -= Time.deltaTime;
            _loadingChangeBGCD -= Time.deltaTime;

            if (_loadingAnimationCD <= 0)
            {
                AnimateText();
                _loadingAnimationCD = _loadingAnimationDelay;
            }

            if (_loadingChangeBGCD <= 0 && _bgSprites.Length > 0)
            {
                var bgSpriteIdx = UnityEngine.Random.Range(0, _bgSprites.Length);
                if (bgSpriteIdx == _currentBgSpriteIdx)
                {
                    bgSpriteIdx++;
                    bgSpriteIdx %= _bgSprites.Length;
                    _currentBgSpriteIdx = bgSpriteIdx;
                }

                _bg.sprite = _bgSprites[_currentBgSpriteIdx];
                _loadingChangeBGCD = _loadingChangeBGDelay;
            }
        }

        private void AnimateText()
        {
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