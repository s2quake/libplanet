using System.Reflection;
using Libplanet.TestUtilities;

namespace Libplanet.Serialization.Tests;

public abstract partial class ModelSerializerTestBase<TData>
{
    [Theory]
    [InlineData(0)]
    [InlineData(1074183504)]
    [InlineData(1849913649)]
    [ClassData(typeof(RandomSeedsData))]
    public void NullableProperty_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedObject = new RecordClassWithNullableProperty(random);
        var serialized = Serialize(expectedObject);
        var actualObject = Deserialize<RecordClassWithNullableProperty>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }

    [Fact]
    public void NullableProperty_WithDefaultValue_SerializeAndDeserialize_Test()
    {
        var expectedObject = new RecordClassWithNullableProperty
        {
            Int32 = default(int),
            Int64 = default(long),
            Biginteger = default(BigInteger),
            Enum = default(TestEnum),
            Boolean = default(bool),
            String = string.Empty,
            DateTimeOffset = default(DateTimeOffset),
            TimeSpan = default(TimeSpan),
        };
        var serialized = Serialize(expectedObject);
        var actualObject = Deserialize<RecordClassWithNullableProperty>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }
}

[Model("Libplanet_Serialization_Tests_ModelSerializerTest_RecordClassWithNullableProperty", Version = 1)]
public sealed record class RecordClassWithNullableProperty : IEquatable<RecordClassWithNullableProperty>
{
    public RecordClassWithNullableProperty()
    {
    }

    public RecordClassWithNullableProperty(Random random)
    {
        Int32_Null = null;
        Int32 = Rand.Int32(random);
        Int64_Null = null;
        Int64 = Rand.Int64(random);
        Biginteger_Null = null;
        Biginteger = Rand.BigInteger(random);
        Enum_Null = null;
        Enum = Rand.Enum<TestEnum>(random);
        Boolean_Null = null;
        Boolean = Rand.Boolean(random);
        String_Null = null;
        String = Rand.String(random);
        DateTimeOffset_Null = null;
        DateTimeOffset = Rand.DateTimeOffset(random);
        TimeSpan_Null = null;
        TimeSpan = Rand.TimeSpan(random);
    }

    [Property(0)] public int? Int32_Null { get; init; }

    [Property(1)] public int? Int32 { get; init; }

    [Property(2)] public long? Int64_Null { get; init; }

    [Property(3)] public long? Int64 { get; init; }

    [Property(4)] public BigInteger? Biginteger_Null { get; init; }

    [Property(5)] public BigInteger? Biginteger { get; init; }

    [Property(6)] public TestEnum? Enum_Null { get; init; }

    [Property(7)] public TestEnum? Enum { get; init; }

    [Property(8)] public bool? Boolean_Null { get; init; }

    [Property(9)] public bool? Boolean { get; init; }

    [Property(10)] public string? String_Null { get; init; }

    [Property(11)] public string? String { get; init; }

    [Property(12)] public DateTimeOffset? DateTimeOffset_Null { get; init; }

    [Property(13)] public DateTimeOffset? DateTimeOffset { get; init; }

    [Property(14)] public TimeSpan? TimeSpan_Null { get; init; }

    [Property(15)] public TimeSpan? TimeSpan { get; init; }

    public bool Equals(RecordClassWithNullableProperty? other) => ModelResolver.Equals(this, other);

    public override int GetHashCode() => ModelResolver.GetHashCode(this);
}
