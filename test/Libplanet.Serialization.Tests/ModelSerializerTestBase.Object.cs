namespace Libplanet.Serialization.Tests;

public abstract partial class ModelSerializerTestBase<TData>
{
    [Fact]
    public void ObjectProperty_SerializeAndDeserialize_Test()
    {
        var expectedObject = new RecordClassWithObject
        {
            Value1 = 1,
            Value2 = null,
            Value3 = [1, "string", true],
            Value4 = [1, null, "string", true],
            Value5 = new KeyValuePair<object, object>(1, "string"),
            Value6 = null,
            Value7 = new Tuple<object, object>(1, "string"),
            Value8 = null,
        };
        var serialized = Serialize(expectedObject);
        var actualObject = Deserialize<RecordClassWithObject>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }
}

[Model("Libplanet_Serialization_Tests_ModelSerializerTest_RecordClassWithObject", Version = 1)]
public sealed record class RecordClassWithObject : IEquatable<RecordClassWithObject>
{
    [Property(0)]
    public required object Value1 { get; init; }

    [Property(1)]
    public required object? Value2 { get; init; }

    [Property(2)]
    public required object[] Value3 { get; init; }

    [Property(3)]
    public required object?[] Value4 { get; init; }

    [Property(4)]
    public required KeyValuePair<object, object> Value5 { get; init; }

    [Property(5)]
    public required KeyValuePair<object, object>? Value6 { get; init; }

    [Property(6)]
    public required Tuple<object, object> Value7 { get; init; }

    [Property(7)]
    public required Tuple<object, object>? Value8 { get; init; }


    public bool Equals(RecordClassWithObject? other) => ModelResolver.Equals(this, other);

    public override int GetHashCode() => ModelResolver.GetHashCode(this);
}
