using System.Globalization;
using System.Linq;
using UnityEngine;

namespace CodexFramework.Utils
{
    public static class VersionUtility
    {
        public static int CompareVersions(string version1, string version2)
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
                var strippedPrev = new string(versionNumbers1[i].Where(char.IsDigit).ToArray());
                var strippedCurr = new string(versionNumbers2[i].Where(char.IsDigit).ToArray());
                var prevNum = int.Parse(strippedPrev, CultureInfo.InvariantCulture);
                var currNum = int.Parse(strippedCurr, CultureInfo.InvariantCulture);
                if (prevNum < currNum)
                    return -1;
                if (prevNum > currNum)
                    return 1;
            }

            return 0;
        }
    }
}
