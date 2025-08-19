namespace Libplanet.Serialization.Tests;

public sealed partial class ModelSerializerTest
{
    public static TheoryData<object> PrimitiveValues =>
    [
        (object)(BigInteger)0,
        (object)(BigInteger)1,
        (object)true,
        (object)false,
        (object)Array.Empty<byte>(),
        (object)new byte[] { 0, 1, 2, 3 },
        (object)DateTimeOffset.MinValue,
        (object)DateTimeOffset.MaxValue,
        (object)ImmutableArray<byte>.Empty,
        (object)ImmutableArray.Create<byte>(0, 1, 2, 3),
        (object)0,
        (object)1,
        (object)0L,
        (object)1L,
        (object)string.Empty,
        (object)"Hello, World!",
        (object)TimeSpan.Zero,
        (object)TimeSpan.FromSeconds(1),
    ];

    public static TheoryData<object> PrimitiveDefaultValues =>
    [
        (object)default(BigInteger),
        (object)default(bool),
        (object)default(DateTimeOffset),
        (object)default(int),
        (object)default(long),
        (object)default(TimeSpan),
    ];

    [Theory]
    [MemberData(nameof(PrimitiveValues))]
    public void PrimitiveValue_SerializeAndDeserialize_Test(object expectedValue)
    {
        var serialized = ModelSerializer.SerializeToBytes(expectedValue);
        var actualValue = ModelSerializer.DeserializeFromBytes(serialized);
        Assert.Equal(expectedValue, actualValue);
    }

    [Theory]
    [MemberData(nameof(PrimitiveDefaultValues))]
    public void PrimitiveDefaultValue_SerializeAndDeserialize_Test(object expectedValue)
    {
        var serialized = ModelSerializer.SerializeToBytes(expectedValue);
        var actualValue = ModelSerializer.DeserializeFromBytes(serialized);
        Assert.True(Equals(expectedValue, actualValue));
    }
}
