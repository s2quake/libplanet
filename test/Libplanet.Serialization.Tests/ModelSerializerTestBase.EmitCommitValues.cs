namespace Libplanet.Serialization.Tests;

public abstract partial class ModelSerializerTestBase<TData>
{
    [Fact]
    public void Model_WithEmitDefaultValuesOptions_SerializeAndDeserialize_Test()
    {
        var expectedObject = new EmitDefaultValuesClass();
        var options = new ModelOptions
        {
            EmitDefaultValues = true,
        };
        var serialized1 = Serialize(expectedObject, options);
        var serialized2 = Serialize(expectedObject);
        Assert.NotEqual(serialized1, serialized2);
        var actualObject1 = Deserialize<EmitDefaultValuesClass>(serialized1);
        var actualObject2 = Deserialize<EmitDefaultValuesClass>(serialized2);
        Assert.Equal(expectedObject, actualObject1);
        Assert.Equal(expectedObject, actualObject2);
    }

    [Fact]
    public void Model_WithEmitDefaultProperty_SerializeAndDeserialize_Test()
    {
        var expectedObject = new EmitDefaultPropertyClass();
        var options = new ModelOptions
        {
            EmitDefaultValues = true,
        };
        var serialized1 = Serialize(expectedObject, options);
        var serialized2 = Serialize(expectedObject);
        Assert.Equal(serialized1, serialized2);
        var actualObject1 = Deserialize<EmitDefaultPropertyClass>(serialized1);
        var actualObject2 = Deserialize<EmitDefaultPropertyClass>(serialized2);
        Assert.Equal(expectedObject, actualObject1);
        Assert.Equal(expectedObject, actualObject2);
    }

    [Fact]
    public void Model_WithEmitDefaultPropertyAndThrow_SerializeAndDeserialize_Test()
    {
        var expectedObject = new EmitDefaultThrowClass();
        var options = new ModelOptions
        {
            EmitDefaultValues = true,
        };
        Assert.Throws<ModelException>(() => Serialize(expectedObject, options));
        var serialized2 = Serialize(expectedObject);
        var actualObject2 = Deserialize<EmitDefaultThrowClass>(serialized2);
        Assert.Equal(expectedObject, actualObject2);
    }
}

[Model("Libplanet_Serialization_Tests_ModelSerializerTest_EmitDefaultValuesClass", Version = 1)]
public sealed record class EmitDefaultValuesClass : IEquatable<EmitDefaultValuesClass>
{
    [Property(0)]
    public int Value1 { get; init; }

    [Property(1)]
    public string Value2 { get; init; } = string.Empty;
}

[Model("Libplanet_Serialization_Tests_ModelSerializerTest_EmitDefaultPropertyClass", Version = 1)]
public sealed record class EmitDefaultPropertyClass : IEquatable<EmitDefaultPropertyClass>
{
    [Property(0, EmitDefaultValue = true)]
    public int Value1 { get; init; }

    [Property(1, EmitDefaultValue = true)]
    public string Value2 { get; init; } = string.Empty;
}

[Model("Libplanet_Serialization_Tests_ModelSerializerTest_EmitDefaultThrowClass", Version = 1)]
public sealed record class EmitDefaultThrowClass : IEquatable<EmitDefaultThrowClass>
{
    [Property(0)]
    public ImmutableArray<int> Value1 { get; init; }

    public bool Equals(EmitDefaultThrowClass? other) => ModelResolver.Equals(this, other);

    public override int GetHashCode() => ModelResolver.GetHashCode(this);
}
