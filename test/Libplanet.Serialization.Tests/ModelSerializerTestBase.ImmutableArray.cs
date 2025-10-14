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
    public void ImmutableArrayProperty_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedObject = new RecordClassWithImmutableArray(random);
        var serialized = Serialize(expectedObject);
        var actualObject = Deserialize<RecordClassWithImmutableArray>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }

    [Theory]
    [InlineData(0)]
    [ClassData(typeof(RandomSeedsData))]
    public void ImmutableArray_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedObject = new RecordClassWithImmutableArray(random);
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

    [Fact]
    public void ImmutableArrayProperty_WithDefaultArray_SerializeAndDeserialize_Test_Throw()
    {
        var expectedObject = new RecordClassWithImmutableArray
        {
            Ints = default,
            Longs = default,
            BigIntegers = default,
            Enums = default,
            Bools = default,
            Strings = default,
            DateTimeOffsets = default,
            TimeSpans = default,
        };
        var serialized = Serialize(expectedObject);
        var actualObject = Deserialize<RecordClassWithImmutableArray>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }
}

[Model("Libplanet_Serialization_Tests_ModelSerializerTest_RecordClassWithImmutableArray", Version = 1)]
public sealed record class RecordClassWithImmutableArray
    : IEquatable<RecordClassWithImmutableArray>
{
    public RecordClassWithImmutableArray()
    {
    }

    public RecordClassWithImmutableArray(Random random)
    {
        Ints = ImmutableArray(random, Int32);
        Longs = ImmutableArray(random, Int64);
        BigIntegers = ImmutableArray(random, BigInteger);
        Enums = ImmutableArray(random, Enum<TestEnum>);
        Bools = ImmutableArray(random, Boolean);
        Strings = ImmutableArray(random, String);
        DateTimeOffsets = ImmutableArray(random, DateTimeOffset);
        TimeSpans = ImmutableArray(random, TimeSpan);
    }

    [Property(0)]
    public ImmutableArray<int> Ints { get; init; } = [1];

    [Property(1)]
    public ImmutableArray<long> Longs { get; init; } = [];

    [Property(2)]
    public ImmutableArray<BigInteger> BigIntegers { get; init; } = [];

    [Property(3)]
    public ImmutableArray<TestEnum> Enums { get; init; } = [];

    [Property(4)]
    public ImmutableArray<bool> Bools { get; init; } = [];

    [Property(5)]
    public ImmutableArray<string> Strings { get; init; } = [];

    [Property(6)]
    public ImmutableArray<DateTimeOffset> DateTimeOffsets { get; init; } = [];

    [Property(7)]
    public ImmutableArray<TimeSpan> TimeSpans { get; init; } = [];

    public bool Equals(RecordClassWithImmutableArray? other) => ModelResolver.Equals(this, other);

    public override int GetHashCode() => ModelResolver.GetHashCode(this);
}
