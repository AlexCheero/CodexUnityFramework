using System;
using UnityEngine;

[RequireComponent(typeof(Canvas))]
public abstract class WindowBehaviour<T> : MonoBehaviour where T : Enum
{
    public event Action<T> OnOpen;
    public event Action<WindowBehaviour<T>> OnClose;
    
    public abstract T GetWindowType();
    
    public Canvas Canvas { get; private set; }

    public virtual void Init() => Canvas = GetComponent<Canvas>();
    public virtual void Show() => gameObject.SetActive(true);
    public virtual void Hide() => gameObject.SetActive(false);
    protected void OnOpenClick(T window) => OnOpen?.Invoke(window);
    public void OnCloseClick() => OnClose?.Invoke(this);
}