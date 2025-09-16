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
    public void ImmutableHashSetProperty_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedObject = new RecordClassWithImmutableHashSet(random);
        var serialized = Serialize(expectedObject);
        var actualObject = Deserialize<RecordClassWithImmutableHashSet>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }
}

[Model(Version = 1, TypeName = "Libplanet_Serialization_Tests_ModelSerializerTest_RecordClassWithImmutableHashSet")]
public sealed record class RecordClassWithImmutableHashSet
    : IEquatable<RecordClassWithImmutableHashSet>
{
    public RecordClassWithImmutableHashSet()
    {
    }

    public RecordClassWithImmutableHashSet(Random random)
    {
        Ints = ImmutableHashSet(random, Int32);
        Longs = ImmutableHashSet(random, Int64);
        BigIntegers = ImmutableHashSet(random, BigInteger);
        Enums = ImmutableHashSet(random, Enum<TestEnum>);
        Bools = ImmutableHashSet(random, Boolean);
        Strings = ImmutableHashSet(random, String);
        DateTimeOffsets = ImmutableHashSet(random, DateTimeOffset);
        TimeSpans = ImmutableHashSet(random, TimeSpan);
    }

    [Property(0)]
    public ImmutableHashSet<int> Ints { get; init; } = [1];

    [Property(1)]
    public ImmutableHashSet<long> Longs { get; init; } = [];

    [Property(2)]
    public ImmutableHashSet<BigInteger> BigIntegers { get; init; } = [];

    [Property(3)]
    public ImmutableHashSet<TestEnum> Enums { get; init; } = [];

    [Property(4)]
    public ImmutableHashSet<bool> Bools { get; init; } = [];

    [Property(5)]
    public ImmutableHashSet<string> Strings { get; init; } = [];

    [Property(6)]
    public ImmutableHashSet<DateTimeOffset> DateTimeOffsets { get; init; } = [];

    [Property(7)]
    public ImmutableHashSet<TimeSpan> TimeSpans { get; init; } = [];

    public bool Equals(RecordClassWithImmutableHashSet? other) => ModelResolver.Equals(this, other);

    public override int GetHashCode() => ModelResolver.GetHashCode(this);
}
