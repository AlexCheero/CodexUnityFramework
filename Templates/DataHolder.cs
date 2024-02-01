using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CodexFramework.Utils;
using Newtonsoft.Json;
using UnityEngine;

namespace CodexFramework.Templates
{
    public class DataHolder : Singleton<DataHolder>
    {
        public StaticData SData;
        private Dictionary<string, string> _userDataDict;

        public UserSettings USettings;

        protected override void Init()
        {
            base.Init();
            Deserialize();
        }

        void OnDestroy()
        {
            //is DontDestroyOnLoad
            if (gameObject.scene.buildIndex == -1)
                Serialize();
        }

        void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
                Serialize();
        }

        private void Deserialize()
        {
            var serializerSettings = new JsonSerializerSettings
            {
                Error = (sender, args) =>
                {
                    Debug.Log(args.ErrorContext.Error.Message);
                    args.ErrorContext.Handled = true;
                }
            };

            if (PlayerPrefs.HasKey(Constants.UserDataKey))
            {
                var json = PlayerPrefs.GetString(Constants.UserDataKey);
                _userDataDict =
                    JsonConvert.DeserializeObject<Dictionary<string, string>>(json, serializerSettings);
            }
            _userDataDict ??= new Dictionary<string, string>();

            //if (ShouldWipe())
            //    Wipe();

            #region UserSettings
            if (_userDataDict.ContainsKey(Constants.MasterVolumeKey))
                USettings.MasterVolume = float.Parse(_userDataDict[Constants.MasterVolumeKey], CultureInfo.InvariantCulture);
            if (_userDataDict.ContainsKey(Constants.MusicVolumeKey))
                USettings.MusicVolume = float.Parse(_userDataDict[Constants.MusicVolumeKey], CultureInfo.InvariantCulture);
            if (_userDataDict.ContainsKey(Constants.SfxVolumeKey))
                USettings.SfxVolume = float.Parse(_userDataDict[Constants.SfxVolumeKey], CultureInfo.InvariantCulture);
            #endregion
        }

        private bool ShouldWipe()
        {
            if (!_userDataDict.ContainsKey(Constants.VersionKey))
                return false;

            var prevVersionNumbers = _userDataDict[Constants.VersionKey].Split('.');
            var currentVersionNumbers = Application.version.Split('.');

            if (prevVersionNumbers.Length != currentVersionNumbers.Length)
            {
                Debug.LogError("corrupted version string");
                return false;
            }

            var isVersionLower = false;
            for (int i = prevVersionNumbers.Length - 1; i >= 0; i--)
            {
                var strippedPrev = new string(prevVersionNumbers[i].Where(c => char.IsDigit(c)).ToArray());
                var strippedCurr = new string(currentVersionNumbers[i].Where(c => char.IsDigit(c)).ToArray());
                var prevNum = int.Parse(strippedPrev, CultureInfo.InvariantCulture);
                var currNum = int.Parse(strippedCurr, CultureInfo.InvariantCulture);
                if (prevNum < currNum)
                {
                    isVersionLower = true;
                    break;
                }
            }

            return isVersionLower;
        }

        private void Wipe()
        {
            _userDataDict.Clear();
            PlayerPrefs.DeleteAll();
        }

        private void Serialize()
        {
            #region UserSettings
            _userDataDict[Constants.MasterVolumeKey] = JsonConvert.SerializeObject(USettings.MasterVolume);
            _userDataDict[Constants.MusicVolumeKey] = JsonConvert.SerializeObject(USettings.MusicVolume);
            _userDataDict[Constants.SfxVolumeKey] = JsonConvert.SerializeObject(USettings.SfxVolume);
            #endregion

            _userDataDict[Constants.VersionKey] = Application.version;

            var json = JsonConvert.SerializeObject(_userDataDict);
            PlayerPrefs.SetString(Constants.UserDataKey, json);
        }
    }
}