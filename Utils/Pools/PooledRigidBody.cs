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
            _rigidbody.velocity = _rigidbody.angularVelocity = Vector3.zero;
            _rigidbody.ResetInertiaTensor();
            _rigidbody.isKinematic = false;
            _rigidbody.WakeUp();
        }

        public void OnReturn()
        {
            _rigidbody.isKinematic = true;
            _rigidbody.Sleep();
        }
    }
}