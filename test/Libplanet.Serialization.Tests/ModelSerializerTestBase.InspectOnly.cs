namespace Libplanet.Serialization.Tests;

public abstract partial class ModelSerializerTestBase<TData>
{
    [Fact]
    public void Model_InspectOnly_SerializeAndDeserialize_Test()
    {
        var expectedObject = new InspectOnlyClass
        {
            Value1 = 1,
            Value2 = 2,
        };
        var options = new ModelOptions
        {
            Purpose = SerializationPurpose.Inspection,
        };
        var serialized = Serialize(expectedObject, options);
        var actualObject = Deserialize<InspectOnlyClass>(serialized);
        Assert.NotEqual(expectedObject, actualObject);
    }

    [Fact]
    public void Model_InspectOnly_WithoutTypeEmission_SerializeAndDeserialize_Test()
    {
        var expectedObject = new InspectOnlyClass
        {
            Value1 = 1,
            Value2 = 2,
        };
        var options = new ModelOptions
        {
            TypeInfoEmission = TypeInfoEmission.Never,
            Purpose = SerializationPurpose.Inspection,
        };
        var serialized = Serialize(expectedObject, options);
        var actualObject = Deserialize<InspectOnlyClass>(serialized, options);
        Assert.NotEqual(expectedObject, actualObject);
    }
}

[Model("Libplanet_Serialization_Tests_ModelSerializerTest_InspectOnlyClass", Version = 1)]
public sealed record class InspectOnlyClass : IEquatable<InspectOnlyClass>
{
    [Property(0)]
    public int Value1 { get; init; }

    [Property(1, InspectOnly = true)]
    public int Value2 { get; init; }
}
