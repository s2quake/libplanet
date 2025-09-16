
namespace Libplanet.Serialization.Tests;

public sealed class ModelSerializerTest(ITestOutputHelper output) : ModelSerializerTestBase<byte[]>(output)
{
    protected override object? Deserialize(byte[] serialized, ModelOptions options)
        => ModelSerializer.Deserialize(serialized, options);

    protected override byte[] Serialize(object? obj, ModelOptions options)
        => ModelSerializer.Serialize(obj, options);
}
