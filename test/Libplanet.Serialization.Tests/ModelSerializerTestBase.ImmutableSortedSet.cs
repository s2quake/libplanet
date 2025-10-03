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
    public void ImmutableSortedSetProperty_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedObject = new RecordClassWithImmutableSortedSet(random);
        var serialized = Serialize(expectedObject);
        var actualObject = Deserialize<RecordClassWithImmutableSortedSet>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }

    [Theory]
    [InlineData(0)]
    [ClassData(typeof(RandomSeedsData))]
    public void ImmutableSortedSet_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedObject = new RecordClassWithImmutableSortedSet(random);
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

[Model("Libplanet_Serialization_Tests_ModelSerializerTest_RecordClassWithImmutableSortedSet", Version = 1)]
public sealed record class RecordClassWithImmutableSortedSet
    : IEquatable<RecordClassWithImmutableSortedSet>
{
    public RecordClassWithImmutableSortedSet()
    {
    }

    public RecordClassWithImmutableSortedSet(Random random)
    {
        Ints = ImmutableSortedSet(random, Int32);
        Longs = ImmutableSortedSet(random, Int64);
        BigIntegers = ImmutableSortedSet(random, BigInteger);
        Enums = ImmutableSortedSet(random, Enum<TestEnum>);
        Bools = ImmutableSortedSet(random, Boolean);
        Strings = ImmutableSortedSet(random, String);
        DateTimeOffsets = ImmutableSortedSet(random, DateTimeOffset);
        TimeSpans = ImmutableSortedSet(random, TimeSpan);
    }

    [Property(0)]
    public ImmutableSortedSet<int> Ints { get; init; } = [1];

    [Property(1)]
    public ImmutableSortedSet<long> Longs { get; init; } = [];

    [Property(2)]
    public ImmutableSortedSet<BigInteger> BigIntegers { get; init; } = [];

    [Property(3)]
    public ImmutableSortedSet<TestEnum> Enums { get; init; } = [];

    [Property(4)]
    public ImmutableSortedSet<bool> Bools { get; init; } = [];

    [Property(5)]
    public ImmutableSortedSet<string> Strings { get; init; } = [];

    [Property(6)]
    public ImmutableSortedSet<DateTimeOffset> DateTimeOffsets { get; init; } = [];

    [Property(7)]
    public ImmutableSortedSet<TimeSpan> TimeSpans { get; init; } = [];

    public bool Equals(RecordClassWithImmutableSortedSet? other) => ModelResolver.Equals(this, other);

    public override int GetHashCode() => ModelResolver.GetHashCode(this);
}
