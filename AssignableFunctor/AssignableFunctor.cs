using System;

namespace CodexFramework.AssignableFunctors
{
    /// <summary>
    /// Non-generic root for SerializeReference + inspector drawer.
    /// Prefer declaring fields as a closed alias (e.g. <c>IntRetIntFunctor</c>) or
    /// a closed generic (<c>AssignableFunctor&lt;int, int&gt;</c>) with [SerializeReference].
    /// </summary>
    [Serializable]
    public abstract class AssignableFunctor
    {
    }

    /// <summary>Serializable Func&lt;TResult&gt; equivalent.</summary>
    [Serializable]
    public abstract class AssignableFunctor<TResult> : AssignableFunctor
    {
        public abstract TResult Invoke();
    }

    /// <summary>Serializable Func&lt;T, TResult&gt; equivalent.</summary>
    [Serializable]
    public abstract class AssignableFunctor<T, TResult> : AssignableFunctor
    {
        public abstract TResult Invoke(T arg);
    }

    /// <summary>Serializable Func&lt;T1, T2, TResult&gt; equivalent.</summary>
    [Serializable]
    public abstract class AssignableFunctor<T1, T2, TResult> : AssignableFunctor
    {
        public abstract TResult Invoke(T1 arg1, T2 arg2);
    }

    /// <summary>Serializable Func&lt;T1, T2, T3, TResult&gt; equivalent.</summary>
    [Serializable]
    public abstract class AssignableFunctor<T1, T2, T3, TResult> : AssignableFunctor
    {
        public abstract TResult Invoke(T1 arg1, T2 arg2, T3 arg3);
    }

    /// <summary>Serializable Func&lt;T1, T2, T3, T4, TResult&gt; equivalent.</summary>
    [Serializable]
    public abstract class AssignableFunctor<T1, T2, T3, T4, TResult> : AssignableFunctor
    {
        public abstract TResult Invoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4);
    }
}
