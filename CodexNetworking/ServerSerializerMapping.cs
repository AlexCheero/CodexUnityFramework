
using CodexECS;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace CodexFramework.Netwroking.Serialization.Server
{
    public static class ServerSerializerMapping
    {
        private static Dictionary<Type, IComponentSerializer> _serializators;

        public static void Init(bool force = false)
        {
            if (_serializators != null && !force)
                return;

            _serializators = new();

            SerializerMapping.Init(typeof(ComponentSerializer<>), force);
            foreach (var componentId in SerializerMapping.SerializedComponents)
            {
                var type = ComponentMapping.GetTypeForId(componentId);
                var serializatorClosedType = typeof(ComponentSerializer<>).MakeGenericType(type);
                _serializators[type] =
                    (IComponentSerializer)Activator.CreateInstance(serializatorClosedType);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IComponentSerializer GetSerializer(int componentId)
        {
#if DEBUG
            if (_serializators == null)
                throw new NetException($"{nameof(ServerSerializerMapping)} is not inited");
#endif

            var type = ComponentMapping.GetTypeForId(componentId);
            return _serializators[type];
        }
    }
}
