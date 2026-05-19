using System;
using UnityEngine;

namespace CodexFramework.WindowingSystem
{
    public abstract class WindowBehaviour<T> : MonoBehaviour where T : Enum
    {
        public abstract T WindowType { get; }
        
        public virtual void Show() => gameObject.SetActive(true);
        public virtual void Hide() => gameObject.SetActive(false);
    }
}