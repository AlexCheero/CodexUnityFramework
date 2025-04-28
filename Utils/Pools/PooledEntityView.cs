using CodexFramework.CodexEcsUnityIntegration.Views;
using UnityEngine;

namespace CodexFramework.Utils.Pools
{
    [RequireComponent(typeof(EntityView))]
    public class PooledEntityView : PooledBehaviour, IResetOnReturnPoolableBehaviour
    {
        private EntityView _view;

        void Awake() => _view = GetComponent<EntityView>();

        public void OnReturn() => _view.DeleteFromWorld();
    }
}