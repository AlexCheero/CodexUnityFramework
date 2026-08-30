using CodexFramework.CodexEcsUnityIntegration.Views;
using UnityEngine;

namespace CodexFramework.Utils.Pools
{
    [RequireComponent(typeof(EntityView))]
    public class PooledEntityView : PooledBehaviour, IResetOnReturnPoolableBehaviour
    {
        [SerializeField]
        private EntityView _view;
        public EntityView View => _view;

        void OnValidate() => _view = GetComponent<EntityView>();

        public void OnReturn() => _view.DeleteFromWorld();
    }
}