
using CodexECS;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace CodexFramework.Netwroking.Serialization.Client
{
    public static class ClientSerializerMapping
    {
        private static Dictionary<Type, IComponentDeserializer> _serializers;

        public static void Init(bool force = false)
        {
            if (_serializers != null && !force)
                return;

            _serializers = new();

            SerializerMapping.Init(typeof(ComponentDeserializer<>), force);
            foreach (var componentId in SerializerMapping.SerializedComponents)
            {
                var type = ComponentMapping.GetTypeForId(componentId);
                var serializatorClosedType = typeof(ComponentDeserializer<>).MakeGenericType(type);
                _serializers[type] =
                    (IComponentDeserializer)Activator.CreateInstance(serializatorClosedType);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IComponentDeserializer GetDeserializer(int componentId)
        {
#if DEBUG
            if (_serializers == null)
                throw new NetException($"{nameof(ClientSerializerMapping)} is not inited");
#endif

            var type = ComponentMapping.GetTypeForId(componentId);
            return _serializers[type];
        }
    }
}
