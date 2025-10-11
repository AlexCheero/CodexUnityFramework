using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class WindowsManager<T> : MonoBehaviour where T : Enum
{
    private Dictionary<T, WindowBehaviour<T>> _windows;
    
    private Stack<WindowBehaviour<T>> _windowsStack;

    private void Awake()
    {
        _windowsStack = new();
        
        var windows = GetComponentsInChildren<WindowBehaviour<T>>(true);
        _windows = new();
        foreach (var window in windows)
        {
            window.Init();
#if DEBUG
            if (_windows.ContainsKey(window.GetWindowType()))
                throw new Exception($"Trying to add {window.name} by key {window.GetWindowType()} but {_windows[window.GetWindowType()].name} is already registered");
#endif
            _windows[window.GetWindowType()] = window;
            window.OnOpen += ShowWindow;
            window.OnClose += HideWindow;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && HideLastWindow()) { }
    }

    private int _sortingOrder;
    
    public void ShowWindow(T windowType)
    {
        var window = _windows[windowType];
        window.transform.SetAsLastSibling();
        window.Canvas.sortingOrder = _sortingOrder;
        window.Show();
        _sortingOrder++;
        
        _windowsStack.Push(window);
        if (_windowsStack.Count == 1)
            OnFirstWindowOpened();
    }

    public bool HideLastWindow()
    {
        if (!_windowsStack.TryPop(out var window))
            return false;
        
        window.transform.SetAsFirstSibling();
        window.Canvas.sortingOrder = -1;
        window.Hide();
        _sortingOrder--;
        
        if (_windowsStack.Count == 0)
            OnLastWindowClosed();
        
#if DEBUG
        if (_sortingOrder < 0)
            throw new Exception("Sorting order dropped below 0");
#endif
        
        return true;
    }

    public void HideWindow(WindowBehaviour<T> window)
    {
        if (!_windowsStack.TryPeek(out var lastWindow) || lastWindow != window)
        {
#if DEBUG
            throw new Exception("Trying to hide not last window");
#else
            return;
#endif
        }

        HideLastWindow();
    }

    protected abstract void OnFirstWindowOpened();

    protected abstract void OnLastWindowClosed();
}