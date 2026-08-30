using CodexFramework.AssignableFunctors;
using UnityEditor;

namespace CodexFramework.SerializeReferenceDrawing.Editor
{
    [CustomPropertyDrawer(typeof(AssignableFunctor), true)]
    [CustomPropertyDrawer(typeof(AssignableFunctor<>), true)]
    [CustomPropertyDrawer(typeof(AssignableFunctor<,>), true)]
    [CustomPropertyDrawer(typeof(AssignableFunctor<,,>), true)]
    [CustomPropertyDrawer(typeof(AssignableFunctor<,,,>), true)]
    [CustomPropertyDrawer(typeof(AssignableFunctor<,,,,>), true)]
    public sealed class AssignableFunctorDrawer : SerializeReferenceDrawer<AssignableFunctor>
    {
    }
}
