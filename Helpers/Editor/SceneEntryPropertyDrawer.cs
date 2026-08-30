using CodexFramework.Scenes;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SceneEntry))]
public class SceneEntryPropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var scenePathProp = property.FindPropertyRelative("ScenePath");

        SceneAsset currentScene = null;
        string currentPath = scenePathProp.stringValue;
        if (!string.IsNullOrEmpty(currentPath))
            currentScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(currentPath);

        EditorGUI.BeginChangeCheck();
        var newScene = (SceneAsset)EditorGUI.ObjectField(
            position,
            label,
            currentScene,
            typeof(SceneAsset),
            allowSceneObjects: false
        );

        if (EditorGUI.EndChangeCheck())
        {
            scenePathProp.stringValue = newScene != null
                ? AssetDatabase.GetAssetPath(newScene)
                : string.Empty;
        }

        EditorGUI.EndProperty();
    }
}
