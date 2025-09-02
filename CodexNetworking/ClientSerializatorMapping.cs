
using CodexECS;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace CodexFramework.Netwroking.Serialization.Client
{
    public static class ClientSerializatorMapping
    {
        private static Dictionary<Type, IComponentDeserializator> _serializators;

        public static void Init(bool force = false)
        {
            if (_serializators != null && !force)
                return;

            _serializators = new();

            SerializatorMapping.Init(typeof(ComponentDeserializator<>), force);
            foreach (var componentId in SerializatorMapping.SerializedComponents)
            {
                var type = ComponentMapping.GetTypeForId(componentId);
                var serializatorClosedType = typeof(ComponentDeserializator<>).MakeGenericType(type);
                _serializators[type] =
                    (IComponentDeserializator)Activator.CreateInstance(serializatorClosedType);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IComponentDeserializator GetSerializator(int componentId)
        {
#if DEBUG
            if (_serializators == null)
                throw new NetException($"{nameof(ClientSerializatorMapping)} is not inited");
#endif

            var type = ComponentMapping.GetTypeForId(componentId);
            return _serializators[type];
        }
    }
}
