namespace Libplanet.Serialization.Tests;

public abstract partial class ModelSerializerTestBase<T>
{
    [Theory]
    [InlineData(0)]
    [InlineData(1074183504)]
    [InlineData(1849913649)]
    [ClassData(typeof(RandomSeedData))]
    public void BigInteger_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedValue = Rand.BigInteger(random);
        var serialized = ModelSerializer.Serialize(expectedValue);
        var actualValue = ModelSerializer.Deserialize(serialized);
        Assert.Equal(expectedValue, actualValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1074183504)]
    [InlineData(1849913649)]
    [ClassData(typeof(RandomSeedData))]
    public void Boolean_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedValue = Rand.Boolean(random);
        var serialized = ModelSerializer.Serialize(expectedValue);
        var actualValue = ModelSerializer.Deserialize(serialized);
        Assert.Equal(expectedValue, actualValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1074183504)]
    [InlineData(1849913649)]
    [ClassData(typeof(RandomSeedData))]
    public void Byte_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedValue = Rand.Byte(random);
        var serialized = ModelSerializer.Serialize(expectedValue);
        var actualValue = ModelSerializer.Deserialize(serialized);
        Assert.Equal(expectedValue, actualValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1074183504)]
    [InlineData(1849913649)]
    [InlineData(2079056856)]
    [ClassData(typeof(RandomSeedData))]
    public void Char_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedValue = Rand.Try(random, Rand.Char, c => !char.IsSurrogate(c));
        var serialized = ModelSerializer.Serialize(expectedValue);
        var actualValue = ModelSerializer.Deserialize(serialized);
        Assert.Equal(expectedValue, actualValue);
    }

    [Fact]
    public void SurrogateChar_SerializeAndDeserialize_Test_Throw()
    {
        var random = Rand.GetRandom(Output);
        var expectedValue = RandomSurrogate(random);
        Assert.Throws<ModelSerializationException>(() => ModelSerializer.Serialize(expectedValue));

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
    [ClassData(typeof(RandomSeedData))]
    public void DateTimeOffset_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedValue = Rand.DateTimeOffset(random);
        var serialized = ModelSerializer.Serialize(expectedValue);
        var actualValue = ModelSerializer.Deserialize(serialized);
        Assert.Equal(expectedValue, actualValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1074183504)]
    [InlineData(1849913649)]
    [ClassData(typeof(RandomSeedData))]
    public void Guid_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedValue = Rand.Guid(random);
        var serialized = ModelSerializer.Serialize(expectedValue);
        var actualValue = ModelSerializer.Deserialize(serialized);
        Assert.Equal(expectedValue, actualValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1074183504)]
    [InlineData(1849913649)]
    [ClassData(typeof(RandomSeedData))]
    public void Int32_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedValue = Rand.Int32(random);
        var serialized = ModelSerializer.Serialize(expectedValue);
        var actualValue = ModelSerializer.Deserialize(serialized);
        Assert.Equal(expectedValue, actualValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1074183504)]
    [InlineData(1849913649)]
    [ClassData(typeof(RandomSeedData))]
    public void Int64_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedValue = Rand.Int64(random);
        var serialized = ModelSerializer.Serialize(expectedValue);
        var actualValue = ModelSerializer.Deserialize(serialized);
        Assert.Equal(expectedValue, actualValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1074183504)]
    [InlineData(1849913649)]
    [ClassData(typeof(RandomSeedData))]
    public void String_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedValue = Rand.String(random);
        var serialized = ModelSerializer.Serialize(expectedValue);
        var actualValue = ModelSerializer.Deserialize(serialized);
        Assert.Equal(expectedValue, actualValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1074183504)]
    [InlineData(1849913649)]
    [ClassData(typeof(RandomSeedData))]
    public void TimeSpan_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedValue = Rand.TimeSpan(random);
        var serialized = ModelSerializer.Serialize(expectedValue);
        var actualValue = ModelSerializer.Deserialize(serialized);
        Assert.Equal(expectedValue, actualValue);
    }
}
