namespace Libplanet.Serialization.Tests;

public sealed partial class ModelSerializerTest
{
    [Fact]
    public void ImmutableArrayDefault_SerializeAndDeserialize_Test()
    {
        var serialized = ModelSerializer.SerializeToBytes(ImmutableArray<byte>.Empty);
        var actualObject = ModelSerializer.DeserializeFromBytes(serialized)!;
        Assert.Equal(ImmutableArray<byte>.Empty, actualObject);
    }
}
