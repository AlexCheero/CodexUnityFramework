using System;
using UnityEngine;

namespace CodexFramework.Templates
{
    [Serializable]
    public struct UserSettings
    {
        [Header("Settings")]
        [Header("SoundSettings")]
        [SerializeField]
        private float _masterVolume;
        [SerializeField]
        private float _musicVolume;
        [SerializeField]
        private float _sfxVolume;

        public float MasterVolume
        {
            get => _masterVolume;
            set => _masterVolume = value;
        }
        public float MusicVolume
        {
            get => _musicVolume;
            set => _musicVolume = value;
        }
        public float SfxVolume
        {
            get => _sfxVolume;
            set => _sfxVolume = value;
        }
    }
}