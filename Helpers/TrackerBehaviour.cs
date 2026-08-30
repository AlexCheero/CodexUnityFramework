using System.Runtime.CompilerServices;
using UnityEngine;

namespace CodexFramework.Helpers.LifetimeTracker
{
    public static class TrackerCreator
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TrackerBehaviour.TrackerBlock GetLifetimeTracker(this GameObject gameObject)
        {
            return gameObject.TryGetComponent<TrackerBehaviour>(out var trackerBehaviour)
                ? trackerBehaviour.Tracker
                : gameObject.AddComponent<TrackerBehaviour>().Tracker;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TrackerBehaviour.TrackerBlock GetLifetimeTracker(this Component component)
        {
            var go = component.gameObject;
            return go.TryGetComponent<TrackerBehaviour>(out var trackerBehaviour)
                ? trackerBehaviour.Tracker
                : go.AddComponent<TrackerBehaviour>().Tracker;
        }
    }
    
    public class TrackerBehaviour : MonoBehaviour
    {
        public class TrackerBlock
        {
            public bool IsAlive = true;
            public bool IsActive = true;

            public bool IsAliveAndActive
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => IsAlive && IsActive;
            }
        }
        
        public TrackerBlock Tracker { get; } = new();

        void OnEnable() => Tracker.IsActive = true;
        void OnDisable() => Tracker.IsActive = false;
        void OnDestroy() => Tracker.IsAlive = Tracker.IsActive = false;
    }
}