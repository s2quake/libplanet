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
    public void ArrayProperty_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedObject = new RecordClassWithArray(random);
        var serialized = Serialize(expectedObject);
        var actualObject = Deserialize<RecordClassWithArray>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }
}

[Model(
    Version = 1,
    TypeName = "Libplanet_Serialization_Tests_ModelSerializerTest_RecordClassWithArray")]
public sealed record class RecordClassWithArray
    : IEquatable<RecordClassWithArray>
{
    public RecordClassWithArray()
    {
    }

    public RecordClassWithArray(Random random)
    {
        Ints = Array(random, Int32);
        Longs = Array(random, Int64);
        BigIntegers = Array(random, BigInteger);
        Enums = Array(random, Enum<TestEnum>);
        Bools = Array(random, Boolean);
        Strings = Array(random, String);
        DateTimeOffsets = Array(random, DateTimeOffset);
        TimeSpans = Array(random, TimeSpan);
    }

    [Property(0)]
    public int[] Ints { get; init; } = [1];

    [Property(1)]
    public long[] Longs { get; init; } = [];

    [Property(2)]
    public BigInteger[] BigIntegers { get; init; } = [];

    [Property(3)]
    public TestEnum[] Enums { get; init; } = [];

    [Property(4)]
    public bool[] Bools { get; init; } = [];

    [Property(5)]
    public string[] Strings { get; init; } = [];

    [Property(6)]
    public DateTimeOffset[] DateTimeOffsets { get; init; } = [];

    [Property(7)]
    public TimeSpan[] TimeSpans { get; init; } = [];

    public bool Equals(RecordClassWithArray? other) => ModelResolver.Equals(this, other);

    public override int GetHashCode() => ModelResolver.GetHashCode(this);
}
