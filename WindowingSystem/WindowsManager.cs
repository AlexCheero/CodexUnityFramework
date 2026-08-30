using System;
using System.Collections.Generic;
using CodexFramework.Utils;
using UnityEngine;

namespace CodexFramework.WindowingSystem
{
    public abstract class WindowsManager<T> : Singleton<WindowsManager<T>> where T : Enum
    {
        private Dictionary<T, WindowBehaviour<T>> _windows;
    
        private Stack<WindowBehaviour<T>> _windowsStack;

        public bool IsAnyOpened => _windowsStack.Count > 0;

        private void Awake()
        {
            _windowsStack = new();
        
            var windows = GetComponentsInChildren<WindowBehaviour<T>>(true);
            _windows = new();
            foreach (var window in windows)
            {
#if DEBUG
                if (_windows.ContainsKey(window.WindowType))
                    throw new Exception($"Trying to add {window.name} by key {window.WindowType} but {_windows[window.WindowType].name} is already registered");
#endif
                _windows[window.WindowType] = window;
            }
        }

        protected virtual void ShowWindow(T windowType)
        {
#if UNITY_EDITOR
            foreach (var wb in _windowsStack)
            {
                if (EqualityComparer<T>.Default.Equals(wb.WindowType, windowType))
                    throw new Exception("Trying to show window that is already in stack");
            }
#endif
            
            if (_windowsStack.TryPeek(out var lastWindow))
                lastWindow.Hide();
        
            var window = _windows[windowType];
            window.transform.SetAsLastSibling();
            window.Show();
        
            _windowsStack.Push(window);
        }

        public virtual bool HideLastWindow()
        {
            if (!_windowsStack.TryPop(out var lastWindow))
                return false;
        
            lastWindow.transform.SetAsFirstSibling();
            lastWindow.Hide();
        
            if (_windowsStack.TryPeek(out var prevWindow))
                prevWindow.Show();
        
            return true;
        }
    }
}