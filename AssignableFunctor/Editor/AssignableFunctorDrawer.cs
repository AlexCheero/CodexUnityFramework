using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace CodexFramework.AssignableFunctors.Editor
{
    [CustomPropertyDrawer(typeof(AssignableFunctor), true)]
    [CustomPropertyDrawer(typeof(AssignableFunctor<>), true)]
    [CustomPropertyDrawer(typeof(AssignableFunctor<,>), true)]
    [CustomPropertyDrawer(typeof(AssignableFunctor<,,>), true)]
    [CustomPropertyDrawer(typeof(AssignableFunctor<,,,>), true)]
    [CustomPropertyDrawer(typeof(AssignableFunctor<,,,,>), true)]
    public class AssignableFunctorDrawer : PropertyDrawer
    {
        private static readonly AdvancedDropdownState DropdownState = new();

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var line = EditorGUIUtility.singleLineHeight;
            if (property.propertyType != SerializedPropertyType.ManagedReference)
                return line * 2f + EditorGUIUtility.standardVerticalSpacing;

            if (property.managedReferenceValue == null || !property.isExpanded)
                return line;

            var height = line;
            var spacing = EditorGUIUtility.standardVerticalSpacing;
            var child = property.Copy();
            var end = property.GetEndProperty();
            if (!child.NextVisible(true))
                return height;

            while (!SerializedProperty.EqualContents(child, end))
            {
                height += spacing + EditorGUI.GetPropertyHeight(child, true);
                if (!child.NextVisible(false))
                    break;
            }

            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                EditorGUI.HelpBox(
                    position,
                    $"{label.text}: use [SerializeReference] on AssignableFunctor fields.",
                    MessageType.Warning
                );
                EditorGUI.EndProperty();
                return;
            }

            var lineHeight = EditorGUIUtility.singleLineHeight;
            var headerRect = new Rect(position.x, position.y, position.width, lineHeight);

            var fieldType = GetDeclaredFieldType(property);
            var currentType = property.managedReferenceValue?.GetType();
            var hasValue = currentType != null;
            var hasChildren = hasValue && HasVisibleChildren(property);

            if (hasChildren)
            {
                var foldoutRect = new Rect(headerRect.x, headerRect.y, 14f, lineHeight);
                property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, GUIContent.none, true);
            }

            var labelRect = new Rect(
                headerRect.x + (hasChildren ? 14f : 0f),
                headerRect.y,
                EditorGUIUtility.labelWidth - (hasChildren ? 14f : 0f),
                lineHeight
            );
            EditorGUI.LabelField(labelRect, label);

            var dropdownRect = new Rect(
                headerRect.x + EditorGUIUtility.labelWidth,
                headerRect.y,
                headerRect.width - EditorGUIUtility.labelWidth,
                lineHeight
            );

            var typeLabel = currentType != null ? currentType.Name : "None";
            if (EditorGUI.DropdownButton(dropdownRect, new GUIContent(typeLabel), FocusType.Keyboard))
            {
                var types = AssignableFunctorTypeCache.GetConcreteTypes(fieldType);
                var propertyPath = property.propertyPath;
                var serializedObject = property.serializedObject;

                var dropdown = new AssignableFunctorDropdown(DropdownState, types, selectedType =>
                {
                    serializedObject.Update();
                    var targetProp = serializedObject.FindProperty(propertyPath);
                    if (targetProp == null)
                        return;

                    targetProp.managedReferenceValue = selectedType != null
                        ? Activator.CreateInstance(selectedType)
                        : null;
                    serializedObject.ApplyModifiedProperties();
                });

                dropdown.Show(dropdownRect);
            }

            if (hasChildren && property.isExpanded)
            {
                var y = headerRect.yMax + EditorGUIUtility.standardVerticalSpacing;
                EditorGUI.indentLevel++;

                var child = property.Copy();
                var end = property.GetEndProperty();
                if (child.NextVisible(true))
                {
                    while (!SerializedProperty.EqualContents(child, end))
                    {
                        var childHeight = EditorGUI.GetPropertyHeight(child, true);
                        var childRect = new Rect(position.x, y, position.width, childHeight);
                        EditorGUI.PropertyField(childRect, child, true);
                        y += childHeight + EditorGUIUtility.standardVerticalSpacing;
                        if (!child.NextVisible(false))
                            break;
                    }
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        private Type GetDeclaredFieldType(SerializedProperty property)
        {
            if (fieldInfo != null)
            {
                var type = UnwrapCollectionElementType(fieldInfo.FieldType);
                if (type != null && typeof(AssignableFunctor).IsAssignableFrom(type))
                    return type;
            }

            return AssignableFunctorTypeCache.ResolveManagedReferenceType(property.managedReferenceFieldTypename)
                   ?? typeof(AssignableFunctor);
        }

        private static Type UnwrapCollectionElementType(Type type)
        {
            if (type == null)
                return null;

            if (type.IsArray)
                return type.GetElementType();

            if (type.IsGenericType)
            {
                var definition = type.GetGenericTypeDefinition();
                if (definition == typeof(List<>) || definition == typeof(IList<>))
                    return type.GetGenericArguments()[0];
            }

            return type;
        }

        private static bool HasVisibleChildren(SerializedProperty property)
        {
            var child = property.Copy();
            var end = property.GetEndProperty();
            if (!child.NextVisible(true))
                return false;
            return !SerializedProperty.EqualContents(child, end);
        }
    }
}
