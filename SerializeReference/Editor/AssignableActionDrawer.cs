using CodexFramework.AssignableFunctors;
using UnityEditor;

namespace CodexFramework.SerializeReferenceDrawing.Editor
{
    [CustomPropertyDrawer(typeof(AssignableAction), true)]
    [CustomPropertyDrawer(typeof(AssignableAction<>), true)]
    [CustomPropertyDrawer(typeof(AssignableAction<,>), true)]
    [CustomPropertyDrawer(typeof(AssignableAction<,,>), true)]
    [CustomPropertyDrawer(typeof(AssignableAction<,,,>), true)]
    public sealed class AssignableActionDrawer : SerializeReferenceDrawer<AssignableAction>
    {
    }
}
