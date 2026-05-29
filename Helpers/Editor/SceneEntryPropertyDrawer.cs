using CodexFramework.Scenes;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SceneEntry))]
public class SceneEntryPropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        SerializedProperty previewProp    = property.FindPropertyRelative("Preview");
        SerializedProperty scenePathProp  = property.FindPropertyRelative("ScenePath");

        float lineH   = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;

        // --- Preview (стандартное поле Sprite) ---
        Rect previewRect = new Rect(position.x, position.y, position.width, lineH);
        EditorGUI.PropertyField(previewRect, previewProp);

        // --- SceneAsset (объект-ссылка только в редакторе) ---
        Rect sceneRect = new Rect(position.x, position.y + lineH + spacing, position.width, lineH);

        // Восстанавливаем SceneAsset из сохранённого пути
        SceneAsset currentScene = null;
        string currentPath = scenePathProp.stringValue;
        if (!string.IsNullOrEmpty(currentPath))
            currentScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(currentPath);

        EditorGUI.BeginChangeCheck();
        var newScene = (SceneAsset)EditorGUI.ObjectField(
            sceneRect,
            new GUIContent("Scene"),
            currentScene,
            typeof(SceneAsset),
            allowSceneObjects: false
        );

        // Если пользователь изменил поле — записываем путь обратно в строку
        if (EditorGUI.EndChangeCheck())
        {
            scenePathProp.stringValue = newScene != null
                ? AssetDatabase.GetAssetPath(newScene)
                : string.Empty;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float lineH   = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        return lineH * 2 + spacing; // Preview + Scene
    }
}