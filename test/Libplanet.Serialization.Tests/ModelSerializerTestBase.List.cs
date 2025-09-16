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
    public void ListProperty_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedObject = new RecordClassWithList(random);
        var serialized = Serialize(expectedObject);
        var actualObject = Deserialize<RecordClassWithList>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }
}

[Model(Version = 1, TypeName = "Libplanet_Serialization_Tests_ModelSerializerTest_RecordClassWithList")]
public sealed record class RecordClassWithList
    : IEquatable<RecordClassWithList>
{
    public RecordClassWithList()
    {
    }

    public RecordClassWithList(Random random)
    {
        Ints = List(random, Int32);
        Longs = List(random, Int64);
        BigIntegers = List(random, BigInteger);
        Enums = List(random, Enum<TestEnum>);
        Bools = List(random, Boolean);
        Strings = List(random, String);
        DateTimeOffsets = List(random, DateTimeOffset);
        TimeSpans = List(random, TimeSpan);
    }

    [Property(0)]
    public List<int> Ints { get; init; } = [1];

    [Property(1)]
    public List<long> Longs { get; init; } = [];

    [Property(2)]
    public List<BigInteger> BigIntegers { get; init; } = [];

    [Property(3)]
    public List<TestEnum> Enums { get; init; } = [];

    [Property(4)]
    public List<bool> Bools { get; init; } = [];

    [Property(5)]
    public List<string> Strings { get; init; } = [];

    [Property(6)]
    public List<DateTimeOffset> DateTimeOffsets { get; init; } = [];

    [Property(7)]
    public List<TimeSpan> TimeSpans { get; init; } = [];

    public bool Equals(RecordClassWithList? other) => ModelResolver.Equals(this, other);

    public override int GetHashCode() => ModelResolver.GetHashCode(this);
}
