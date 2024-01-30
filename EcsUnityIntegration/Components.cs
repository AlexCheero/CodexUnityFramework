using UnityEngine;

namespace CodexFramework.EcsUnityIntegration.Components
{
    public interface IComponent { }

    public struct CollisionComponent : IComponent
    {
        public Vector3 contactPoint;
        public Collider collider;
    }

    public struct TriggerEnterComponent : IComponent
    {
        public Collider collider;
    }

    public struct TriggerExitComponent : IComponent
    {
        public Collider collider;
    }
}

namespace CodexFramework.EcsUnityIntegration.Tags
{
    public interface ITag { }

    public struct OverrideCollision : ITag { }
    public struct OverrideTriggerEnter : ITag { }
    public struct OverrideTriggerExit : ITag { }
}