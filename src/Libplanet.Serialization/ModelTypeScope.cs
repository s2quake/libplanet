namespace Libplanet.Serialization;

internal static class ModelTypeScope
{
    private static readonly ThreadLocal<Stack<Properties>?> _stack = new();

    public static Type CurrentType => Current.Type;

    public static Properties Current => _stack.Value is { Count: > 0 } s ? s.Peek() : new Properties(typeof(object));

    public static IDisposable Push(Type type, bool emitDefaultValue = false)
    {
        var s = _stack.Value ??= new Stack<Properties>();
        s.Push(new Properties(type, emitDefaultValue));
        return new PopOnDispose(s);
    }

    public static bool CanOmitTypeInfo(Type type, ModelOptions options)
    {
        if (options.TypeInfoEmission is TypeInfoEmission.Always)
        {
            return false;
        }

        if (options.TypeInfoEmission is TypeInfoEmission.Never)
        {
            return true;
        }

        return CurrentType.IsSealed && type == CurrentType;
    }

    public static bool CanWriteDefaultValue(ModelOptions options)
        => !options.EmitDefaultValues && !Current.EmitDefaultValue;

    private sealed class PopOnDispose(Stack<Properties> stack) : IDisposable
    {
        public void Dispose()
        {
            if (stack is { Count: > 0 })
            {
                stack.Pop();
            }
        }
    }

    public record class Properties(Type Type, bool EmitDefaultValue = false)
    {
    }
}
