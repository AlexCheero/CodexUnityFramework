using System;

namespace CodexFramework.AssignableFunctors
{
    /// <summary>
    /// Action-like serializable hierarchy. Mark fields with [SerializeReference].
    /// Drawer: AssignableActionDrawer (inherits SerializeReferenceDrawer&lt;AssignableAction&gt;).
    /// Prefer closed aliases (e.g. <c>IntAction</c>) as field types.
    /// Parameterless actions: subclass <see cref="AssignableAction"/> directly (e.g. <c>VoidAction</c>)
    /// and declare <c>Invoke()</c>.
    /// </summary>
    [Serializable]
    public abstract class AssignableAction
    {
    }

    /// <summary>Serializable Action&lt;T&gt; equivalent.</summary>
    [Serializable]
    public abstract class AssignableAction<T> : AssignableAction
    {
        public abstract void Invoke(T arg);
    }

    /// <summary>Serializable Action&lt;T1, T2&gt; equivalent.</summary>
    [Serializable]
    public abstract class AssignableAction<T1, T2> : AssignableAction
    {
        public abstract void Invoke(T1 arg1, T2 arg2);
    }

    /// <summary>Serializable Action&lt;T1, T2, T3&gt; equivalent.</summary>
    [Serializable]
    public abstract class AssignableAction<T1, T2, T3> : AssignableAction
    {
        public abstract void Invoke(T1 arg1, T2 arg2, T3 arg3);
    }

    /// <summary>Serializable Action&lt;T1, T2, T3, T4&gt; equivalent.</summary>
    [Serializable]
    public abstract class AssignableAction<T1, T2, T3, T4> : AssignableAction
    {
        public abstract void Invoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4);
    }
}
