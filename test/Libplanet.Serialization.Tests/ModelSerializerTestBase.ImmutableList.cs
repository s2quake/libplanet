#pragma warning disable SA1414 // Tuple types in signatures should have element names
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
    public void ImmutableListProperty_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedObject = new RecordClassWithImmutableList(random);
        var serialized = Serialize(expectedObject);
        var actualObject = Deserialize<RecordClassWithImmutableList>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }
}

[Model(Version = 1, TypeName = "Libplanet_Serialization_Tests_ModelSerializerTest_RecordClassWithImmutableList")]
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
