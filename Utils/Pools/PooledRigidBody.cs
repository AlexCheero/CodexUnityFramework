using UnityEngine;

namespace CodexFramework.Utils.Pools
{
    [RequireComponent(typeof(Rigidbody))]
    public class PooledRigidBody : PooledBehaviour, IResetOnGetPoolableBehaviour, IResetOnReturnPoolableBehaviour
    {
        private Rigidbody _rigidbody;
        
        void Awake() => _rigidbody = GetComponent<Rigidbody>();
        
        public void OnGet()
        {
            _rigidbody.isKinematic = false;
            _rigidbody.linearVelocity = _rigidbody.angularVelocity = Vector3.zero;
        }

        public void OnReturn() => _rigidbody.isKinematic = true;
    }
}