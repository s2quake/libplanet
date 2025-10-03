namespace Libplanet.Serialization;

public static class ModelTypeScope
{
    private static readonly ThreadLocal<Stack<Type>?> _stack = new();

    public static Type Current => _stack.Value is { Count: > 0 } s ? s.Peek() : typeof(object);

    public static IDisposable Push(Type options)
    {
        var s = _stack.Value ??= new Stack<Type>();
        s.Push(options);
        return new PopOnDispose(s);
    }

    public static bool CanOmitTypeInfo(Type type) => CanOmitTypeInfo(type, ModelOptionsScope.Current);

    public static bool CanOmitTypeInfo(Type type, ModelOptions options)
    {
        if (options.TypeInfoMode is ModelTypeInfoMode.Always)
        {
            return false;
        }

        if (options.TypeInfoMode is ModelTypeInfoMode.Never)
        {
            return true;
        }

        return Current.IsSealed && type == Current;
    }

    private sealed class PopOnDispose(Stack<Type> s) : IDisposable
    {
        public void Dispose()
        {
            if (s is { Count: > 0 })
            {
                s.Pop();
            }
        }
    }
}