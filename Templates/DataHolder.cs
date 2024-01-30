using System.Collections.Generic;
using System.Globalization;
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

            //if (!_userDataDict.ContainsKey(ConstantsTemplate.VersionKey))
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

        private void Wipe()
        {
            var previousVersion = int.Parse(_userDataDict[Constants.VersionKey], CultureInfo.InvariantCulture);
            var currentVersion = int.Parse(Application.version, CultureInfo.InvariantCulture);

            if (previousVersion >= currentVersion)
                return;

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

            var json = JsonConvert.SerializeObject(_userDataDict);
            PlayerPrefs.SetString(Constants.UserDataKey, json);
            PlayerPrefs.SetString(Constants.VersionKey, Application.version);
        }
    }
}