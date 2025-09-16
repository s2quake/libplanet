using Libplanet.TestUtilities;
using static Libplanet.TestUtilities.RandomUtility;

namespace Libplanet.Serialization.Tests;

public abstract partial class ModelSerializerTestBase<T>
{
    [Theory]
    [InlineData(0)]
    [InlineData(1074183504)]
    [InlineData(1849913649)]
    [ClassData(typeof(RandomSeedData))]
    public void ImmutableArrayProperty_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedObject = new RecordClassWithImmutableArray(random);
        var serialized = Serialize(expectedObject);
        var actualObject = Deserialize<RecordClassWithImmutableArray>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }

    [Fact]
    public void ImmutableArrayProperty_WithDefault_SerializeAndDeserialize_Test()
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

[Model(
    Version = 1,
    TypeName = "Libplanet_Serialization_Tests_ModelSerializerTest_RecordClassWithImmutableArray")]
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
