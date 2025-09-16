using Libplanet.Serialization.Tests;

namespace Libplanet.Serialization.Json.Tests;

public sealed partial class ModelJsonSerializerTest(ITestOutputHelper output)
    : ModelSerializerTestBase<string>(output)
{
    protected override object? Deserialize(string serialized, ModelOptions options)
        => ModelJsonSerializer.Deserialize(serialized, options);

    protected override string Serialize(object? obj, ModelOptions options)
        => ModelJsonSerializer.Serialize(obj, options);
}
