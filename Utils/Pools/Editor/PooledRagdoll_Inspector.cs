using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(CodexFramework.Utils.Pools.PooledRagdoll))]
public class PooledRagdoll_Inspector : Editor
{
    private GUIStyle _dirtyMessageStyle;
    private bool _isDirty;
    
    private CodexFramework.Utils.Pools.PooledRagdoll Ragdoll => target as CodexFramework.Utils.Pools.PooledRagdoll;

    public override VisualElement CreateInspectorGUI()
    {
        _dirtyMessageStyle = new GUIStyle();
        _dirtyMessageStyle.normal.textColor = Color.red;
        _isDirty = !Ragdoll.Check();
        return base.CreateInspectorGUI();
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (!_isDirty)
            return;
        
        EditorGUILayout.LabelField("ragdoll structure changed", _dirtyMessageStyle);
        if (!GUILayout.Button(new GUIContent("Recache data")))
            return;
        
        Ragdoll.RecacheData();
        _isDirty = !Ragdoll.Check();
    }
}
