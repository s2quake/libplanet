namespace Libplanet.Serialization.Tests;

public abstract partial class ModelSerializerTestBase<T>
{
    [Fact]
    public void NullValue_Test()
    {
        var bytes = ModelSerializer.Serialize(null);
        Assert.Equal([0], bytes);
    }

    [Fact]
    public void ZeroByte_Deserialize_ThrowTest()
    {
        byte[] bytes = [];
        Assert.Throws<ModelSerializationException>(() => ModelSerializer.Deserialize(bytes));
    }
}
