using System;
using UnityEngine;

namespace CodexFramework.WindowingSystem
{
    public abstract class WindowBehaviour<T> : MonoBehaviour where T : Enum
    {
        [SerializeField]
        protected T _window;
    
        public virtual T WindowType => _window;
        
        public virtual void Show() => gameObject.SetActive(true);
        public virtual void Hide() => gameObject.SetActive(false);
    }
}