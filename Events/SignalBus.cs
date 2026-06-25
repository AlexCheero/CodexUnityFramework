using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodexFramework.Events
{
    /// <summary>
    /// Strongly-typed global signal hub, the C# analog to Godot's signals.
    /// Any code can <see cref="Emit{T}"/> a signal and any listener can
    /// <see cref="Subscribe{T}"/> / <see cref="Unsubscribe{T}"/> by signal type,
    /// without the emitter and listener referencing each other.
    /// </summary>
    public static class SignalBus
    {
        private static readonly List<Action> Resetters = new();

        private static class Channel<T>
        {
            public static Action<T> Handlers;

            static Channel() => Resetters.Add(() => Handlers = null);
        }

        public static void Subscribe<T>(Action<T> handler) => Channel<T>.Handlers += handler;

        public static void Unsubscribe<T>(Action<T> handler) => Channel<T>.Handlers -= handler;

        public static void Emit<T>(T signal) => Channel<T>.Handlers?.Invoke(signal);

        // Clears stale handlers when entering Play Mode with domain reload disabled,
        // so subscriptions never leak between play sessions in the editor.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            foreach (var reset in Resetters)
                reset();
        }
    }
}
