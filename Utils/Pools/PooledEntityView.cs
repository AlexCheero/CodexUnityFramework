using CodexFramework.CodexEcsUnityIntegration.Views;
using UnityEngine;

namespace CodexFramework.Utils.Pools
{
    [RequireComponent(typeof(EntityView))]
    public class PooledEntityView : PoolItem
    {
        public EntityView View { get; private set; }

        public void InitView() => View = GetComponent<EntityView>();

        void Awake() => InitView();

        public override void ReturnToPool()
        {
            base.ReturnToPool();
            View.DeleteFromWorld();
        }
    }
}