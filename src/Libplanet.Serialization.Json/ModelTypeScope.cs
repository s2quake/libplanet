#if MODEL_JSON
namespace Libplanet.Serialization.Json;
#elif MODEL_YAML
namespace Libplanet.Serialization.Yaml;
#else
#error This file must be included in either Libplanet.Serialization.Json or Libplanet.Serialization.Yaml project.
#endif

internal static class ModelTypeScope
{
    private static readonly ThreadLocal<Stack<Type>?> _stack = new();

    public static Type Current => _stack.Value is { Count: > 0 } s ? s.Peek() : typeof(object);

    public static IDisposable Push(Type type)
    {
        var s = _stack.Value ??= new Stack<Type>();
        s.Push(type);
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

    private sealed class PopOnDispose(Stack<Type> stack) : IDisposable
    {
        public void Dispose()
        {
            if (stack is { Count: > 0 })
            {
                stack.Pop();
            }
        }
    }
}
