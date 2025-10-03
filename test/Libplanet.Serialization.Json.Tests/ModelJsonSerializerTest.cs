using Libplanet.Serialization.Tests;

namespace Libplanet.Serialization.Json.Tests;

public sealed partial class ModelJsonSerializerTest(ITestOutputHelper output)
    : ModelSerializerTestBase<string>(output)
{
    protected override object? Deserialize(string serialized, Type type, ModelOptions options)
        => ModelJsonSerializer.Deserialize(serialized, type, options);

    protected override string Serialize(object? obj, Type type, ModelOptions options)
        => ModelJsonSerializer.Serialize(obj, type, options);
}
