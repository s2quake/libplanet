namespace Libplanet.Serialization.Tests;

public abstract partial class ModelSerializerTestBase<TData>(ITestOutputHelper output)
    where TData : notnull
{
    protected ITestOutputHelper Output { get; } = output;

    protected TData Serialize<T>(T? obj)
        where T : notnull
        => Serialize(obj, ModelOptions.Empty);

    protected TData Serialize<T>(T? obj, ModelOptions options)
        where T : notnull
        => Serialize(obj, obj?.GetType() ?? typeof(T), options);

    protected TData Serialize(object? obj, Type type) => Serialize(obj, type, ModelOptions.Empty);

    protected abstract TData Serialize(object? obj, Type type, ModelOptions options);

    protected object? Deserialize(TData data) => Deserialize(data, ModelOptions.Empty);

    protected object? Deserialize(TData data, ModelOptions options)
        => Deserialize(data, typeof(object), options);

    protected object? Deserialize(TData data, Type type)
        => Deserialize(data, type, ModelOptions.Empty);

    protected abstract object? Deserialize(TData data, Type type, ModelOptions options);

    protected T Deserialize<T>(TData data)
        where T : notnull
        => Deserialize<T>(data, ModelOptions.Empty);

    protected T Deserialize<T>(TData data, ModelOptions options)
        where T : notnull
    {
        if (Deserialize(data, typeof(T), options) is T obj)
        {
            return obj;
        }

        throw new InvalidOperationException("Failed to deserialize");
    }
}
