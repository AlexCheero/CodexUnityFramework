using System;
using UnityEngine;

namespace CodexFramework.Helpers
{
    [RequireComponent(typeof(Collider))]
    public class CallbackCollision : MonoBehaviour
    {
        public event Action<Collider> OnTriggerEnterCallback;
        public event Action<Collider> OnTriggerExitCallback;
        public event Action<Collision> OnCollisionEnterCallback;
        public event Action<Collision> OnCollisionExitCallback;

        void OnTriggerEnter(Collider other) => OnTriggerEnterCallback?.Invoke(other);
        void OnTriggerExit(Collider other) => OnTriggerExitCallback?.Invoke(other);
        void OnCollisionEnter(Collision collision) => OnCollisionEnterCallback?.Invoke(collision);
        void OnCollisionExit(Collision collision) => OnCollisionExitCallback?.Invoke(collision);
    }
}