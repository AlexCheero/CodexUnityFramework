using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CodexFramework.SerializeReferenceDrawing.Editor
{
    internal static class SerializeReferenceTypeCache
    {
        private static readonly Dictionary<Type, Type[]> Cache = new();

        public static Type[] GetConcreteTypes(Type fieldType)
        {
            if (fieldType == null || fieldType == typeof(object))
                return Array.Empty<Type>();

            if (Cache.TryGetValue(fieldType, out var cached))
                return cached;

            var derived = TypeCache.GetTypesDerivedFrom(fieldType)
                .Where(IsSelectableType);

            var list = new List<Type>();
            if (IsSelectableType(fieldType))
                list.Add(fieldType);
            list.AddRange(derived);

            var result = list
                .Distinct()
                .OrderBy(t => t.Name, StringComparer.Ordinal)
                .ToArray();

            Cache[fieldType] = result;
            return result;
        }

        public static void Clear() => Cache.Clear();

        private static bool IsSelectableType(Type type)
        {
            if (type == null || type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
                return false;
            if (typeof(Object).IsAssignableFrom(type))
                return false;
            if (type.IsValueType)
                return false;
            // SerializeReference requires [Serializable] on concrete implementations.
            if (!type.IsDefined(typeof(SerializableAttribute), inherit: false))
                return false;
            if (type.GetCustomAttribute<ObsoleteAttribute>() != null)
                return false;

            var ctor = type.GetConstructor(Type.EmptyTypes);
            return ctor != null && ctor.IsPublic;
        }

        public static Type ResolveManagedReferenceType(string managedReferenceTypeName)
        {
            if (string.IsNullOrEmpty(managedReferenceTypeName))
                return null;

            // Format: "AssemblyName Full.Type.Name"
            var splitIndex = managedReferenceTypeName.IndexOf(' ');
            if (splitIndex <= 0 || splitIndex >= managedReferenceTypeName.Length - 1)
                return null;

            var assemblyName = managedReferenceTypeName.Substring(0, splitIndex);
            var typeName = managedReferenceTypeName.Substring(splitIndex + 1);
            return Type.GetType($"{typeName}, {assemblyName}");
        }
    }
}
