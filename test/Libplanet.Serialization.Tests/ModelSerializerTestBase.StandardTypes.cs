using Libplanet.TestUtilities;

namespace Libplanet.Serialization.Tests;

public abstract partial class ModelSerializerTestBase<TData>
{
    [Theory]
    [ClassData(typeof(StandardTypeData))]
    public void StandardType_SerializeAndDeserialize_Test(object expectedValue)
    {
        var serialized = Serialize(expectedValue);
        var actualValue = Deserialize(serialized);
        Assert.Equal(expectedValue, actualValue);
    }

    [Theory]
    [ClassData(typeof(StandardTypeData))]
    public void StandardType_WithNullable_SerializeAndDeserialize_Test(object expectedValue)
    {
        if (expectedValue.GetType().IsValueType)
        {
            var type = typeof(Nullable<>).MakeGenericType(expectedValue.GetType());
            var serialized = Serialize(expectedValue, type);
            var actualValue = Deserialize(serialized);
            Assert.Equal(expectedValue, actualValue);
        }
    }

    [Theory]
    [ClassData(typeof(StandardTypeDefaultData))]
    public void StandardType_WithDefault_SerializeAndDeserialize_Test(object expectedValue)
    {
        var serialized = Serialize(expectedValue);
        var actualValue = Deserialize(serialized);
        Assert.True(Equals(expectedValue, actualValue));
    }

    [Fact]
    public void StandardType_WithDefaultArray_SerializeAndDeserialize_Test()
    {
        var expectedValue = default(ImmutableArray<int>);
        var serialized = Serialize(expectedValue);
        var actualValue = Deserialize(serialized);
        Assert.True(Equals(expectedValue, actualValue));
    }

    [Theory]
    [ClassData(typeof(StandardTypeDefaultData))]
    public void StandardType_WithDefaultNullable_SerializeAndDeserialize_Test(object expectedValue)
    {
        if (expectedValue.GetType().IsValueType)
        {
            var type = typeof(Nullable<>).MakeGenericType(expectedValue.GetType());
            var serialized = Serialize(expectedValue, type);
            var actualValue = Deserialize(serialized);
            Assert.True(Equals(expectedValue, actualValue));
        }
    }

    [Theory]
    [InlineData(0)]
    [ClassData(typeof(RandomSeedsData))]
    public void BigInteger_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedValue = Rand.BigInteger(random);
        var serialized1 = Serialize(expectedValue);
        var actualValue1 = Deserialize(serialized1);
        Assert.Equal(expectedValue, actualValue1);

        var serialized2 = Serialize(expectedValue, typeof(BigInteger));
        var actualValue2 = Deserialize<BigInteger>(serialized2);
        Assert.Equal(expectedValue, actualValue2);

        var options = new ModelOptions { TypeInfoEmission = TypeInfoEmission.Never };
        var serialized3 = Serialize(expectedValue, options);
        var actualValue3 = Deserialize<BigInteger>(serialized3, options);
        Assert.Equal(expectedValue, actualValue3);

        var serialized4 = Serialize(expectedValue, options);
        Assert.ThrowsAny<Exception>(() => Deserialize(serialized4, options));
    }

    [Theory]
    [InlineData(0)]
    [ClassData(typeof(RandomSeedsData))]
    public void Boolean_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedValue = Rand.Boolean(random);
        var serialized1 = Serialize(expectedValue);
        var actualValue1 = Deserialize(serialized1);
        Assert.Equal(expectedValue, actualValue1);

        var serialized2 = Serialize(expectedValue, typeof(bool));
        var actualValue2 = Deserialize<bool>(serialized2);
        Assert.Equal(expectedValue, actualValue2);

        var options = new ModelOptions { TypeInfoEmission = TypeInfoEmission.Never };
        var serialized3 = Serialize(expectedValue, options);
        var actualValue3 = Deserialize<bool>(serialized3, options);
        Assert.Equal(expectedValue, actualValue3);

        var serialized4 = Serialize(expectedValue, options);
        Assert.ThrowsAny<Exception>(() => Deserialize(serialized4, options));
    }

    [Theory]
    [InlineData(0)]
    [ClassData(typeof(RandomSeedsData))]
    public void Byte_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedValue = Rand.Byte(random);
        var serialized1 = Serialize(expectedValue);
        var actualValue1 = Deserialize(serialized1);
        Assert.Equal(expectedValue, actualValue1);

        var serialized2 = Serialize(expectedValue, typeof(byte));
        var actualValue2 = Deserialize<byte>(serialized2);
        Assert.Equal(expectedValue, actualValue2);

        var options = new ModelOptions { TypeInfoEmission = TypeInfoEmission.Never };
        var serialized3 = Serialize(expectedValue, options);
        var actualValue3 = Deserialize<byte>(serialized3, options);
        Assert.Equal(expectedValue, actualValue3);

        var serialized4 = Serialize(expectedValue, options);
        Assert.ThrowsAny<Exception>(() => Deserialize(serialized4, options));
    }

    [Theory]
    [InlineData(0)]
    [ClassData(typeof(RandomSeedsData))]
    public void Char_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedValue = Rand.Try(random, Rand.Char, c => !char.IsSurrogate(c));
        var serialized1 = Serialize(expectedValue);
        var actualValue1 = Deserialize(serialized1);
        Assert.Equal(expectedValue, actualValue1);

        var serialized2 = Serialize(expectedValue, typeof(char));
        var actualValue2 = Deserialize<char>(serialized2);
        Assert.Equal(expectedValue, actualValue2);

        var options = new ModelOptions { TypeInfoEmission = TypeInfoEmission.Never };
        var serialized3 = Serialize(expectedValue, options);
        var actualValue3 = Deserialize<char>(serialized3, options);
        Assert.Equal(expectedValue, actualValue3);

        var serialized4 = Serialize(expectedValue, options);
        Assert.ThrowsAny<Exception>(() => Deserialize(serialized4, options));
    }

    [Fact]
    public void SurrogateChar_SerializeAndDeserialize_Test_Throw()
    {
        var random = Rand.GetRandom(Output);
        var expectedValue = RandomSurrogate(random);
        Assert.Throws<ModelException>(() => Serialize(expectedValue));

        static char RandomHighSurrogate(Random random)
            => (char)random.Next(0xD800, 0xDBFF + 1);

        static char RandomLowSurrogate(Random random)
            => (char)random.Next(0xDC00, 0xDFFF + 1);

        static char RandomSurrogate(Random random)
            => random.Next(2) == 0 ? RandomHighSurrogate(random) : RandomLowSurrogate(random);
    }

    [Theory]
    [InlineData(0)]
    [ClassData(typeof(RandomSeedsData))]
    public void DateTimeOffset_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedValue = Rand.DateTimeOffset(random);
        var serialized1 = Serialize(expectedValue);
        var actualValue1 = Deserialize(serialized1);
        Assert.Equal(expectedValue, actualValue1);

        var serialized2 = Serialize(expectedValue, typeof(DateTimeOffset));
        var actualValue2 = Deserialize<DateTimeOffset>(serialized2);
        Assert.Equal(expectedValue, actualValue2);

        var options = new ModelOptions { TypeInfoEmission = TypeInfoEmission.Never };
        var serialized3 = Serialize(expectedValue, options);
        var actualValue3 = Deserialize<DateTimeOffset>(serialized3, options);
        Assert.Equal(expectedValue, actualValue3);

        var serialized4 = Serialize(expectedValue, options);
        Assert.ThrowsAny<Exception>(() => Deserialize(serialized4, options));
    }

    [Theory]
    [InlineData(0)]
    [ClassData(typeof(RandomSeedsData))]
    public void Guid_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedValue = Rand.Guid(random);
        var serialized1 = Serialize(expectedValue);
        var actualValue1 = Deserialize(serialized1);
        Assert.Equal(expectedValue, actualValue1);

        var serialized2 = Serialize(expectedValue, typeof(Guid));
        var actualValue2 = Deserialize<Guid>(serialized2);
        Assert.Equal(expectedValue, actualValue2);

        var options = new ModelOptions { TypeInfoEmission = TypeInfoEmission.Never };
        var serialized3 = Serialize(expectedValue, options);
        var actualValue3 = Deserialize<Guid>(serialized3, options);
        Assert.Equal(expectedValue, actualValue3);

        var serialized4 = Serialize(expectedValue, options);
        Assert.ThrowsAny<Exception>(() => Deserialize(serialized4, options));
    }

    [Theory]
    [InlineData(0)]
    [ClassData(typeof(RandomSeedsData))]
    public void Int32_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedValue = Rand.Int32(random);
        var serialized1 = Serialize(expectedValue);
        var actualValue1 = Deserialize(serialized1);
        Assert.Equal(expectedValue, actualValue1);

        var serialized2 = Serialize(expectedValue, typeof(int));
        var actualValue2 = Deserialize<int>(serialized2);
        Assert.Equal(expectedValue, actualValue2);

        var options = new ModelOptions { TypeInfoEmission = TypeInfoEmission.Never };
        var serialized3 = Serialize(expectedValue, options);
        var actualValue3 = Deserialize<int>(serialized3, options);
        Assert.Equal(expectedValue, actualValue3);

        var serialized4 = Serialize(expectedValue, options);
        Assert.ThrowsAny<Exception>(() => Deserialize(serialized4, options));
    }

    [Theory]
    [InlineData(0)]
    [ClassData(typeof(RandomSeedsData))]
    public void Int64_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedValue = Rand.Int64(random);
        var serialized1 = Serialize(expectedValue);
        var actualValue1 = Deserialize(serialized1);
        Assert.Equal(expectedValue, actualValue1);

        var serialized2 = Serialize(expectedValue, typeof(long));
        var actualValue2 = Deserialize<long>(serialized2);
        Assert.Equal(expectedValue, actualValue2);

        var options = new ModelOptions { TypeInfoEmission = TypeInfoEmission.Never };
        var serialized3 = Serialize(expectedValue, options);
        var actualValue3 = Deserialize<long>(serialized3, options);
        Assert.Equal(expectedValue, actualValue3);

        var serialized4 = Serialize(expectedValue, options);
        Assert.ThrowsAny<Exception>(() => Deserialize(serialized4, options));
    }

    [Theory]
    [InlineData(0)]
    [ClassData(typeof(RandomSeedsData))]
    public void String_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedValue = Rand.String(random);
        var serialized1 = Serialize(expectedValue);
        var actualValue1 = Deserialize(serialized1);
        Assert.Equal(expectedValue, actualValue1);

        var serialized2 = Serialize(expectedValue, typeof(string));
        var actualValue2 = Deserialize<string>(serialized2);
        Assert.Equal(expectedValue, actualValue2);

        var options = new ModelOptions { TypeInfoEmission = TypeInfoEmission.Never };
        var serialized3 = Serialize(expectedValue, options);
        var actualValue3 = Deserialize<string>(serialized3, options);
        Assert.Equal(expectedValue, actualValue3);

        var serialized4 = Serialize(expectedValue, options);
        Assert.ThrowsAny<Exception>(() => Deserialize(serialized4, options));
    }

    [Theory]
    [InlineData(0)]
    [ClassData(typeof(RandomSeedsData))]
    public void TimeSpan_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedValue = Rand.TimeSpan(random);
        var serialized1 = Serialize(expectedValue);
        var actualValue1 = Deserialize(serialized1);
        Assert.Equal(expectedValue, actualValue1);

        var serialized2 = Serialize(expectedValue, typeof(TimeSpan));
        var actualValue2 = Deserialize<TimeSpan>(serialized2);
        Assert.Equal(expectedValue, actualValue2);

        var options = new ModelOptions { TypeInfoEmission = TypeInfoEmission.Never };
        var serialized3 = Serialize(expectedValue, options);
        var actualValue3 = Deserialize<TimeSpan>(serialized3, options);
        Assert.Equal(expectedValue, actualValue3);

        var serialized4 = Serialize(expectedValue, options);
        Assert.ThrowsAny<Exception>(() => Deserialize(serialized4, options));
    }
}
