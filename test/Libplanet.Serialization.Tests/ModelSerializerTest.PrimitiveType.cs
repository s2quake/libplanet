namespace Libplanet.Serialization.Tests;

public sealed partial class ModelSerializerTest
{
    public static IEnumerable<object[]> PrimitiveValues =>
    [
        [(BigInteger)0],
        [(BigInteger)1],
        [true],
        [false],
        [Array.Empty<byte>()],
        [new byte[] { 0, 1, 2, 3 }],
        [DateTimeOffset.MinValue],
        [DateTimeOffset.MaxValue],
        [ImmutableArray<byte>.Empty],
        [ImmutableArray.Create<byte>(0, 1, 2, 3)],
        [0],
        [1],
        [0L],
        [1L],
        [string.Empty],
        ["Hello, World!"],
        [TimeSpan.Zero],
        [TimeSpan.FromSeconds(1)],
    ];

    public static IEnumerable<object[]> PrimitiveDefaultValues =>
    [
        [default(BigInteger)],
        [default(bool)],
        [default(ImmutableArray<byte>)],
        [default(DateTimeOffset)],
        [default(int)],
        [default(long)],
        [default(TimeSpan)],
    ];

    [Theory]
    [MemberData(nameof(PrimitiveValues))]
    public void PrimitiveValue_SerializeAndDeserialize_Test(object expectedValue)
    {
        var serialized = ModelSerializer.Serialize(expectedValue);
        var actualValue = ModelSerializer.Deserialize(serialized);
        Assert.Equal(expectedValue, actualValue);
    }

    [Theory]
    [MemberData(nameof(PrimitiveDefaultValues))]
    public void PrimitiveDefaultValue_SerializeAndDeserialize_Test(object expectedValue)
    {
        var serialized = ModelSerializer.Serialize(expectedValue);
        var actualValue = ModelSerializer.Deserialize(serialized);
        Assert.True(Equals(expectedValue, actualValue));
    }
}
