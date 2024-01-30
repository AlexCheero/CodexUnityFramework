using CodexFramework.Utils;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CodexFramework.Gameplay.UI
{
    public class LoadingScreen : Singleton<LoadingScreen>
    {
        [SerializeField]
        private TextMeshProUGUI _loadingText;
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

        void Awake()
        {
            _bg = GetComponent<Image>();
            _bg.sprite = _bgSprites[0];

            _initialLoadingString = _loadingText.text;
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
                var bgSpriteIdx = Random.Range(0, _bgSprites.Length);
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
            _loadingText.text = _loadingStringBuilder.ToString();
        }
    }
}