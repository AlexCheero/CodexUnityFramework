using System;

namespace CodexFramework.AssignableFunctors
{
    // Closed aliases keep field declarations short and reliable with [SerializeReference].
    // Add more the same way:
    //   [Serializable] public abstract class MyAlias : AssignableFunctor<In, Out> { }
    // Concrete classes extending the alias appear in the inspector dropdown.

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
