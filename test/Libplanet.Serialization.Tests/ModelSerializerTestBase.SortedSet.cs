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
    public void SortedSetProperty_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedObject = new RecordClassWithSortedSet(random);
        var serialized = Serialize(expectedObject);
        var actualObject = Deserialize<RecordClassWithSortedSet>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }
}

[Model(Version = 1, TypeName = "Libplanet_Serialization_Tests_ModelSerializerTest_RecordClassWithSortedSet")]
public sealed record class RecordClassWithSortedSet
    : IEquatable<RecordClassWithSortedSet>
{
    public RecordClassWithSortedSet()
    {
    }

    public RecordClassWithSortedSet(Random random)
    {
        Ints = SortedSet(random, Int32);
        Longs = SortedSet(random, Int64);
        BigIntegers = SortedSet(random, BigInteger);
        Enums = SortedSet(random, Enum<TestEnum>);
        Bools = SortedSet(random, Boolean);
        Strings = SortedSet(random, String);
        DateTimeOffsets = SortedSet(random, DateTimeOffset);
        TimeSpans = SortedSet(random, TimeSpan);
    }

    [Property(0)]
    public SortedSet<int> Ints { get; init; } = [1];

    [Property(1)]
    public SortedSet<long> Longs { get; init; } = [];

    [Property(2)]
    public SortedSet<BigInteger> BigIntegers { get; init; } = [];

    [Property(3)]
    public SortedSet<TestEnum> Enums { get; init; } = [];

    [Property(4)]
    public SortedSet<bool> Bools { get; init; } = [];

    [Property(5)]
    public SortedSet<string> Strings { get; init; } = [];

    [Property(6)]
    public SortedSet<DateTimeOffset> DateTimeOffsets { get; init; } = [];

    [Property(7)]
    public SortedSet<TimeSpan> TimeSpans { get; init; } = [];

    public bool Equals(RecordClassWithSortedSet? other) => ModelResolver.Equals(this, other);

    public override int GetHashCode() => ModelResolver.GetHashCode(this);
}
