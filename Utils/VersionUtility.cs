using System;
using System.Globalization;
using System.Linq;

namespace CodexFramework.Utils
{
    public static class VersionUtility
    {
        public static int CompareVersions(string version1, string version2)
        {
            var versionNumbers1 = ParseComponents(version1);
            var versionNumbers2 = ParseComponents(version2);

            for (int i = 0; i < Math.Max(versionNumbers1.Length, versionNumbers2.Length); i++)
            {
                var prevNum = i < versionNumbers1.Length ? versionNumbers1[i] : 0;
                var currNum = i < versionNumbers2.Length ? versionNumbers2[i] : 0;
                if (prevNum < currNum)
                    return -1;
                if (prevNum > currNum)
                    return 1;
            }

            return 0;
        }

        private static int[] ParseComponents(string version) => version.Split('.')
            .Select(component => int.Parse(component, NumberStyles.None, CultureInfo.InvariantCulture))
            .ToArray();
    }
}
