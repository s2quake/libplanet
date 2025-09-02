using Libplanet.TestUtilities;

namespace Libplanet.Serialization.Tests;

public sealed partial class ModelSerializerTest
{
    [Theory]
    [InlineData(0)]
    [InlineData(1074183504)]
    [InlineData(1849913649)]
    [MemberData(nameof(RandomSeeds))]
    public void BigInteger_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedValue = RandomUtility.BigInteger(random);
        var serialized = ModelSerializer.SerializeToBytes(expectedValue);
        var actualValue = ModelSerializer.DeserializeFromBytes(serialized);
        Assert.Equal(expectedValue, actualValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1074183504)]
    [InlineData(1849913649)]
    [MemberData(nameof(RandomSeeds))]
    public void Boolean_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedValue = RandomUtility.Boolean(random);
        var serialized = ModelSerializer.SerializeToBytes(expectedValue);
        var actualValue = ModelSerializer.DeserializeFromBytes(serialized);
        Assert.Equal(expectedValue, actualValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1074183504)]
    [InlineData(1849913649)]
    [MemberData(nameof(RandomSeeds))]
    public void Byte_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedValue = RandomUtility.Byte(random);
        var serialized = ModelSerializer.SerializeToBytes(expectedValue);
        var actualValue = ModelSerializer.DeserializeFromBytes(serialized);
        Assert.Equal(expectedValue, actualValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1074183504)]
    [InlineData(1849913649)]
    [InlineData(2079056856)]
    [MemberData(nameof(RandomSeeds))]
    public void Char_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedValue = RandomUtility.Try(random, RandomUtility.Char, c => !char.IsSurrogate(c));
        var serialized = ModelSerializer.SerializeToBytes(expectedValue);
        var actualValue = ModelSerializer.DeserializeFromBytes(serialized);
        Assert.Equal(expectedValue, actualValue);
    }

    [Fact]
    public void SurrogateChar_SerializeAndDeserialize_Test_Throw()
    {
        var random = RandomUtility.GetRandom(output);
        var expectedValue = RandomSurrogate(random);
        Assert.Throws<ModelSerializationException>(() => ModelSerializer.SerializeToBytes(expectedValue));

        static char RandomHighSurrogate(Random random)
            => (char)random.Next(0xD800, 0xDBFF + 1);

        static char RandomLowSurrogate(Random random)
            => (char)random.Next(0xDC00, 0xDFFF + 1);

        static char RandomSurrogate(Random random)
            => random.Next(2) == 0 ? RandomHighSurrogate(random) : RandomLowSurrogate(random);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1074183504)]
    [InlineData(1849913649)]
    [MemberData(nameof(RandomSeeds))]
    public void DateTimeOffset_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedValue = RandomUtility.DateTimeOffset(random);
        var serialized = ModelSerializer.SerializeToBytes(expectedValue);
        var actualValue = ModelSerializer.DeserializeFromBytes(serialized);
        Assert.Equal(expectedValue, actualValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1074183504)]
    [InlineData(1849913649)]
    [MemberData(nameof(RandomSeeds))]
    public void Guid_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedValue = RandomUtility.Guid(random);
        var serialized = ModelSerializer.SerializeToBytes(expectedValue);
        var actualValue = ModelSerializer.DeserializeFromBytes(serialized);
        Assert.Equal(expectedValue, actualValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1074183504)]
    [InlineData(1849913649)]
    [MemberData(nameof(RandomSeeds))]
    public void Int32_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedValue = RandomUtility.Int32(random);
        var serialized = ModelSerializer.SerializeToBytes(expectedValue);
        var actualValue = ModelSerializer.DeserializeFromBytes(serialized);
        Assert.Equal(expectedValue, actualValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1074183504)]
    [InlineData(1849913649)]
    [MemberData(nameof(RandomSeeds))]
    public void Int64_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedValue = RandomUtility.Int64(random);
        var serialized = ModelSerializer.SerializeToBytes(expectedValue);
        var actualValue = ModelSerializer.DeserializeFromBytes(serialized);
        Assert.Equal(expectedValue, actualValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1074183504)]
    [InlineData(1849913649)]
    [MemberData(nameof(RandomSeeds))]
    public void String_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedValue = RandomUtility.String(random);
        var serialized = ModelSerializer.SerializeToBytes(expectedValue);
        var actualValue = ModelSerializer.DeserializeFromBytes(serialized);
        Assert.Equal(expectedValue, actualValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1074183504)]
    [InlineData(1849913649)]
    [MemberData(nameof(RandomSeeds))]
    public void TimeSpan_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedValue = RandomUtility.TimeSpan(random);
        var serialized = ModelSerializer.SerializeToBytes(expectedValue);
        var actualValue = ModelSerializer.DeserializeFromBytes(serialized);
        Assert.Equal(expectedValue, actualValue);
    }
}
