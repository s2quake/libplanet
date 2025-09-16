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
    public void ImmutableSortedSetProperty_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedObject = new RecordClassWithImmutableSortedSet(random);
        var serialized = Serialize(expectedObject);
        var actualObject = Deserialize<RecordClassWithImmutableSortedSet>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }
}

[Model(Version = 1, TypeName = "Libplanet_Serialization_Tests_ModelSerializerTest_RecordClassWithImmutableSortedSet")]
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
