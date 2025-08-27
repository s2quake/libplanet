namespace Libplanet.Serialization.Tests;

public sealed partial class ModelSerializerTest
{
    [Fact]
    public void NullValue_Test()
    {
        var bytes = ModelSerializer.SerializeToBytes(null);
        Assert.Equal([0], bytes);
    }

    [Fact]
    public void ZeroByte_Deserialize_ThrowTest()
    {
        byte[] bytes = [];
        Assert.Throws<ModelSerializationException>(() => ModelSerializer.DeserializeFromBytes(bytes));
    }
}
