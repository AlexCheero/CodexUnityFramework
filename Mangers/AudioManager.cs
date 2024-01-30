using CodexFramework.Utils;
using Gameplay;
using UnityEngine;
using UnityEngine.Audio;

namespace CodexFramework.Mangers
{
    public class AudioManager : Singleton<AudioManager>
    {
        private const float MinVolume = 0.0001f;

        [SerializeField]
        private AudioMixer _mixer;

        void Start()
        {
            ResetVolume();
        }

        private float GetVolumeParam(float volume) => Mathf.Log10(Mathf.Max(MinVolume, volume)) * 20;

        private void ResetVolume()
        {
            var userSettings = DataHolder.Instance.UserSettings;
            _mixer.SetFloat(Constants.MasterVolume, GetVolumeParam(userSettings.MasterVolume));
            _mixer.SetFloat(Constants.MusicVolume, GetVolumeParam(userSettings.MusicVolume));
            _mixer.SetFloat(Constants.SFXVolume, GetVolumeParam(userSettings.SfxVolume));
        }

        public void Mute()
        {
            _mixer.SetFloat(Constants.MasterVolume, GetVolumeParam(MinVolume));
            _mixer.SetFloat(Constants.MusicVolume, GetVolumeParam(MinVolume));
            _mixer.SetFloat(Constants.SFXVolume, GetVolumeParam(MinVolume));
        }

        public void Unmute() => ResetVolume();

        public void SetMasterVolume(float volume)
        {
            var dh = DataHolder.Instance;
            dh.UserSettings.MasterVolume = volume;
            dh.Serialize();
            _mixer.SetFloat(Constants.MasterVolume, GetVolumeParam(volume));
        }

        public void SetMusicVolume(float volume)
        {
            var dh = DataHolder.Instance;
            dh.UserSettings.MusicVolume = volume;
            dh.Serialize();
            _mixer.SetFloat(Constants.MusicVolume, GetVolumeParam(volume));
        }

        public void SetSFXVolume(float volume)
        {
            var dh = DataHolder.Instance;
            dh.UserSettings.SfxVolume = volume;
            dh.Serialize();
            _mixer.SetFloat(Constants.SFXVolume, GetVolumeParam(volume));
        }
    }
}