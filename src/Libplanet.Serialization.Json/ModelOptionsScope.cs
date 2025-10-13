#if MODEL_JSON
namespace Libplanet.Serialization.Json;
#elif MODEL_YAML
namespace Libplanet.Serialization.Yaml;
#else
#error This file must be included in either Libplanet.Serialization.Json or Libplanet.Serialization.Yaml project.
#endif

internal static class ModelOptionsScope
{
    private static readonly ThreadLocal<Stack<ModelOptions>?> _stack = new();

    public static ModelOptions Current => _stack.Value is { Count: > 0 } s ? s.Peek() : ModelOptions.Empty;

    public static IDisposable Push(ModelOptions options)
    {
        var s = _stack.Value ??= new Stack<ModelOptions>();
        s.Push(options);
        return new PopOnDispose(s);
    }

    private sealed class PopOnDispose(Stack<ModelOptions> s) : IDisposable
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