
using CodexECS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace CodexFramework.Netwroking.Serialization
{
    public static class SerializatorMapping
    {
        private static Dictionary<Type, IComponentSerializator> _serializators;

        public static readonly BitMask SerializedComponents;
        public static void Init(bool force = false)
        {
            if (_serializators != null && !force)
                return;

            _serializators = new();

            // Get all currently loaded assemblies
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                Type[] types = Array.Empty<Type>();
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    // If some types cannot be loaded, use the ones that can
                    types = ex.Types.Where(t => t != null).ToArray();
                }

                foreach (var type in types)
                {
                    if (type.IsAbstract) // skip abstract types
                        continue;

                    // Look at all interfaces this type implements
                    foreach (var iface in type.GetInterfaces())
                    {
                        if (iface.IsGenericType &&
                            iface.GetGenericTypeDefinition() == typeof(ISerializedComponent<>))
                        {
                            // Check CRTP condition: ISerializedComponent<MyComponent>
                            var genericArg = iface.GetGenericArguments()[0];
                            if (genericArg == type)
                            {
                                var componentId = ComponentMapping.GetIdForType(type);
                                SerializedComponents.Set(componentId);
                                var serializatorClosedType = typeof(ComponentSerializator<>).MakeGenericType(type);
                                _serializators[type] =
                                    (IComponentSerializator)Activator.CreateInstance(serializatorClosedType);
                            }
                        }
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IComponentSerializator GetSerializator(int componentId)
        {
            var type = ComponentMapping.GetTypeForId(componentId);
            //should be already inited
            //if (!_serializators.ContainsKey(type))
            //{
            //    var closedType = typeof(Serializator<>).MakeGenericType(type);
            //    _serializators[type] = (ISerializator)Activator.CreateInstance(closedType);
            //}

            return _serializators[type];
        }
    }
}
