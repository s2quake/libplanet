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
    public void HashSetProperty_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedObject = new RecordClassWithHashSet(random);
        var serialized = Serialize(expectedObject);
        var actualObject = Deserialize<RecordClassWithHashSet>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }

    [Theory]
    [InlineData(0)]
    [ClassData(typeof(RandomSeedsData))]
    public void HashSet_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedObject = new RecordClassWithHashSet(random);
        var properties = ModelResolver.GetProperties(expectedObject.GetType());
        foreach (var property in properties)
        {
            var expectedValue = property.GetValue(expectedObject);
            var serialized1 = Serialize(expectedValue);
            var actualValue1 = Deserialize(serialized1);
            Assert.Equal(expectedValue, actualValue1);

            var options = new ModelOptions { TypeInfoMode = ModelTypeInfoMode.Never };
            var serialized2 = Serialize(expectedValue, options);
            var actualValue2 = Deserialize(serialized2, property.PropertyType, options);
            Assert.Equal(expectedValue, actualValue2);

            var serialized3 = Serialize(expectedValue, options);
            Assert.ThrowsAny<ModelException>(() => Deserialize(serialized3, options));
        }
    }
}

[Model("Libplanet_Serialization_Tests_ModelSerializerTest_RecordClassWithHashSet", Version = 1)]
public sealed record class RecordClassWithHashSet
    : IEquatable<RecordClassWithHashSet>
{
    public RecordClassWithHashSet()
    {
    }

    public RecordClassWithHashSet(Random random)
    {
        Ints = HashSet(random, Int32);
        Longs = HashSet(random, Int64);
        BigIntegers = HashSet(random, BigInteger);
        Enums = HashSet(random, Enum<TestEnum>);
        Bools = HashSet(random, Boolean);
        Strings = HashSet(random, String);
        DateTimeOffsets = HashSet(random, DateTimeOffset);
        TimeSpans = HashSet(random, TimeSpan);
    }

    [Property(0)]
    public HashSet<int> Ints { get; init; } = [1];

    [Property(1)]
    public HashSet<long> Longs { get; init; } = [];

    [Property(2)]
    public HashSet<BigInteger> BigIntegers { get; init; } = [];

    [Property(3)]
    public HashSet<TestEnum> Enums { get; init; } = [];

    [Property(4)]
    public HashSet<bool> Bools { get; init; } = [];

    [Property(5)]
    public HashSet<string> Strings { get; init; } = [];

    [Property(6)]
    public HashSet<DateTimeOffset> DateTimeOffsets { get; init; } = [];

    [Property(7)]
    public HashSet<TimeSpan> TimeSpans { get; init; } = [];

    public bool Equals(RecordClassWithHashSet? other) => ModelResolver.Equals(this, other);

    public override int GetHashCode() => ModelResolver.GetHashCode(this);
}
