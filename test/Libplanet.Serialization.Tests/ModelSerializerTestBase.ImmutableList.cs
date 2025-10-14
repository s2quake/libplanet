#pragma warning disable SA1414 // Tuple types in signatures should have element names
using Libplanet.TestUtilities;
using static Libplanet.TestUtilities.RandomUtility;

namespace Libplanet.Serialization.Tests;

public abstract partial class ModelSerializerTestBase<TData>
{
    [Theory]
    [InlineData(0)]
    [InlineData(1074183504)]
    [InlineData(1849913649)]
    [ClassData(typeof(RandomSeedsData))]
    public void ImmutableListProperty_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedObject = new RecordClassWithImmutableList(random);
        var serialized = Serialize(expectedObject);
        var actualObject = Deserialize<RecordClassWithImmutableList>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }

    [Theory]
    [InlineData(0)]
    [ClassData(typeof(RandomSeedsData))]
    public void ImmutableList_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedObject = new RecordClassWithImmutableList(random);
        var properties = ModelResolver.GetProperties(expectedObject.GetType());
        foreach (var property in properties)
        {
            var expectedValue = property.GetValue(expectedObject);
            var serialized1 = Serialize(expectedValue);
            var actualValue1 = Deserialize(serialized1);
            Assert.Equal(expectedValue, actualValue1);

            var options = new ModelOptions { TypeInfoEmission = TypeInfoEmission.Never };
            var serialized2 = Serialize(expectedValue, options);
            var actualValue2 = Deserialize(serialized2, property.PropertyType, options);
            Assert.Equal(expectedValue, actualValue2);

            var serialized3 = Serialize(expectedValue, options);
            Assert.ThrowsAny<ModelException>(() => Deserialize(serialized3, options));
        }
    }
}

[Model("Libplanet_Serialization_Tests_ModelSerializerTest_RecordClassWithImmutableList", Version = 1)]
public sealed record class RecordClassWithImmutableList
    : IEquatable<RecordClassWithImmutableList>
{
    public RecordClassWithImmutableList()
    {
    }

    public RecordClassWithImmutableList(Random random)
    {
        Ints = ImmutableList(random, Int32);
        Longs = ImmutableList(random, Int64);
        BigIntegers = ImmutableList(random, BigInteger);
        Enums = ImmutableList(random, Enum<TestEnum>);
        Bools = ImmutableList(random, Boolean);
        Strings = ImmutableList(random, String);
        DateTimeOffsets = ImmutableList(random, DateTimeOffset);
        TimeSpans = ImmutableList(random, TimeSpan);
    }

    [Property(0)]
    public ImmutableList<int> Ints { get; init; } = [1];

    [Property(1)]
    public ImmutableList<long> Longs { get; init; } = [];

    [Property(2)]
    public ImmutableList<BigInteger> BigIntegers { get; init; } = [];

    [Property(3)]
    public ImmutableList<TestEnum> Enums { get; init; } = [];

    [Property(4)]
    public ImmutableList<bool> Bools { get; init; } = [];

    [Property(5)]
    public ImmutableList<string> Strings { get; init; } = [];

    [Property(6)]
    public ImmutableList<DateTimeOffset> DateTimeOffsets { get; init; } = [];

    [Property(7)]
    public ImmutableList<TimeSpan> TimeSpans { get; init; } = [];

    public bool Equals(RecordClassWithImmutableList? other) => ModelResolver.Equals(this, other);

    public override int GetHashCode() => ModelResolver.GetHashCode(this);
}
