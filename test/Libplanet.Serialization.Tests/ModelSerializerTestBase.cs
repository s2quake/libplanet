namespace Libplanet.Serialization.Tests;

public abstract partial class ModelSerializerTestBase<T>(ITestOutputHelper output)
    where T : notnull
{
    protected ITestOutputHelper Output { get; } = output;

    protected abstract T Serialize(object? obj, ModelOptions options);

    protected T Serialize(object? obj) => Serialize(obj, new());

    protected abstract object? Deserialize(T serialized, ModelOptions options);

    protected object? Deserialize(T serialized) => Deserialize(serialized, new());

    protected U Deserialize<U>(T serialized)
        where U : notnull
        => Deserialize<U>(serialized, new());

    protected U Deserialize<U>(T serialized, ModelOptions options)
        where U : notnull
    {
        if (Deserialize(serialized, options) is U obj)
        {
            return obj;
        }

        throw new InvalidOperationException("Failed to deserialize");
    }
}
