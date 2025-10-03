using Libplanet.Serialization.Tests;

namespace Libplanet.Serialization.Yaml.Tests;

public sealed partial class ModelYamlSerializerTest(ITestOutputHelper output)
    : ModelSerializerTestBase<string>(output)
{
    protected override object? Deserialize(string serialized, Type type, ModelOptions options)
        => ModelYamlSerializer.Deserialize(serialized, type, options);

    protected override string Serialize(object? obj, Type type, ModelOptions options)
        => ModelYamlSerializer.Serialize(obj, type, options);
}
