using System;

namespace CodexFramework.AssignableFunctors
{
    // Closed aliases make inspector typing reliable and keep field declarations short.
    // Add new aliases the same way for any generic argument set you need:
    //   [Serializable] public abstract class FloatRetBoolFunctor : AssignableFunctor<float, bool> { }
    // Then implement concrete classes that extend the alias (those show up in the dropdown).

    [Serializable]
    public abstract class IntRetIntFunctor : AssignableFunctor<int, int>
    {
    }

    [Serializable]
    public abstract class FloatRetFloatFunctor : AssignableFunctor<float, float>
    {
    }

    [Serializable]
    public abstract class BoolRetBoolFunctor : AssignableFunctor<bool, bool>
    {
    }

    [Serializable]
    public abstract class IntRetBoolFunctor : AssignableFunctor<int, bool>
    {
    }

    [Serializable]
    public abstract class FloatRetBoolFunctor : AssignableFunctor<float, bool>
    {
    }

    [Serializable]
    public abstract class StringRetStringFunctor : AssignableFunctor<string, string>
    {
    }
}
