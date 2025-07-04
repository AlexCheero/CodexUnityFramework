using UnityEngine;

namespace CodexFramework.Utils
{
    public static class GameObjectExtensions
    {
        public static void SetStaticRecursive(this GameObject obj, bool isStatic)
        {
            obj.isStatic = isStatic;
            foreach (Transform child in obj.transform)
                SetStaticRecursive(child.gameObject, isStatic);
        }
    }
}