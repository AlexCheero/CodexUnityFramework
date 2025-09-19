
using CodexECS;
using System;
using System.Linq;
using System.Reflection;

namespace CodexFramework.Netwroking.Serialization
{
    public static class SerializerMapping
    {
        public static readonly BitMask SerializedComponents;
        public static void Init(Type serializatorGenericType, bool force = false)
        {
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
                        if (!iface.IsGenericType ||
                            iface.GetGenericTypeDefinition() != typeof(ISerializedComponent<>))
                        {
                            continue;
                        }

                        // Check CRTP condition: ISerializedComponent<MyComponent>
                        var genericArg = iface.GetGenericArguments()[0];
                        if (genericArg != type)
                            continue;
                        
                        var componentId = ComponentMapping.GetIdForType(type);
                        //TODO: make sure that component ids is equal on both client and server
                        SerializedComponents.Set(componentId);
                    }
                }
            }
        }
    }
}
