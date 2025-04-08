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

        private int CompareVersions(string version1, string version2)
        {
            var versionNumbers1 = version1.Split('.');
            var versionNumbers2 = version2.Split('.');
            if (versionNumbers1.Length != versionNumbers2.Length)
            {
                Debug.LogError($"corrupted version string. v1: {version1}, v2: {version2}");
                return -1;
            }

            for (int i = versionNumbers1.Length - 1; i >= 0; i--)
            {
                var strippedPrev = new string(versionNumbers1[i].Where(c => char.IsDigit(c)).ToArray());
                var strippedCurr = new string(versionNumbers2[i].Where(c => char.IsDigit(c)).ToArray());
                var prevNum = int.Parse(strippedPrev, CultureInfo.InvariantCulture);
                var currNum = int.Parse(strippedCurr, CultureInfo.InvariantCulture);
                if (prevNum < currNum)
                    return -1;
                if (prevNum > currNum)
                    return 1;
            }

            return 0;
        }
        
        private bool ShouldWipe() =>
            PlayerPrefs.HasKey(Constants.VersionKey) && CompareVersions(PlayerPrefs.GetString(Constants.VersionKey), Application.version) < 0;

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