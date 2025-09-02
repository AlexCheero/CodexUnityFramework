
using CodexECS;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace CodexFramework.Netwroking.Serialization.Server
{
    public static class ServerSerializatorMapping
    {
        private static Dictionary<Type, IComponentSerializator> _serializators;

        public static void Init(bool force = false)
        {
            if (_serializators != null && !force)
                return;

            _serializators = new();

            SerializatorMapping.Init(typeof(ComponentSerializator<>), force);
            foreach (var componentId in SerializatorMapping.SerializedComponents)
            {
                var type = ComponentMapping.GetTypeForId(componentId);
                var serializatorClosedType = typeof(ComponentSerializator<>).MakeGenericType(type);
                _serializators[type] =
                    (IComponentSerializator)Activator.CreateInstance(serializatorClosedType);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IComponentSerializator GetSerializator(int componentId)
        {
#if DEBUG
            if (_serializators == null)
                throw new NetException($"{nameof(ServerSerializatorMapping)} is not inited");
#endif

            var type = ComponentMapping.GetTypeForId(componentId);
            return _serializators[type];
        }
    }
}
