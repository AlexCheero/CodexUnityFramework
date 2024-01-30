using CodexFramework.EcsUnityIntegration.Views;
using UnityEngine;

namespace CodexFramework.EcsUnityIntegration
{
    public class EntityViewChild : MonoBehaviour
    {
        [SerializeField]
        private EntityView _ownerView;

        public EntityView OwnerView => _ownerView;
    }
}