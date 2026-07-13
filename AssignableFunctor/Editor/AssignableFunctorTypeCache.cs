using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace CodexFramework.AssignableFunctors.Editor
{
    internal static class AssignableFunctorTypeCache
    {
        private static readonly Dictionary<Type, Type[]> Cache = new();

        public static Type[] GetConcreteTypes(Type fieldType)
        {
            if (fieldType == null)
                return Array.Empty<Type>();

            if (Cache.TryGetValue(fieldType, out var cached))
                return cached;

            var derived = TypeCache.GetTypesDerivedFrom(fieldType)
                .Where(IsConcreteAssignableFunctor)
                .OrderBy(t => t.Name, StringComparer.Ordinal);

            var list = new List<Type>();
            if (IsConcreteAssignableFunctor(fieldType))
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

        private static bool IsConcreteAssignableFunctor(Type type)
        {
            if (type == null || type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
                return false;
            return typeof(AssignableFunctor).IsAssignableFrom(type);
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
