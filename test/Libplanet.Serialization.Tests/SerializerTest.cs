namespace Libplanet.Serialization.Tests;

public sealed partial class SerializerTest
{
    public enum TestEnum
    {
        A,
        B,
        C,
    }

    public static IEnumerable<object[]> RandomSeeds =>
    [
        [Random.Shared.Next()],
        [Random.Shared.Next()],
        [Random.Shared.Next()],
        [Random.Shared.Next()],
    ];

    [Theory]
    [InlineData(typeof(byte))]
    [InlineData(typeof(bool))]
    [InlineData(typeof(string))]
    [InlineData(typeof(int))]
    [InlineData(typeof(long))]
    [InlineData(typeof(BigInteger))]
    [InlineData(typeof(byte[]))]
    [InlineData(typeof(TestEnum))]
    [InlineData(typeof(DateTimeOffset))]
    [InlineData(typeof(TimeSpan))]
    public void CanSupport_Test(Type type)
    {
        Assert.True(ModelSerializer.CanSupportType(type));
    }

    [Theory]
    [InlineData(typeof(bool[]))]
    [InlineData(typeof(string[]))]
    [InlineData(typeof(int[]))]
    [InlineData(typeof(long[]))]
    [InlineData(typeof(BigInteger[]))]
    [InlineData(typeof(byte[][]))]
    [InlineData(typeof(TestEnum[]))]
    [InlineData(typeof(DateTimeOffset[]))]
    [InlineData(typeof(TimeSpan[]))]
    public void CanSupportArray_Test(Type type)
    {
        Assert.True(ModelSerializer.CanSupportType(type));
    }

    [Theory]
    [InlineData(typeof(ImmutableArray<bool>))]
    [InlineData(typeof(ImmutableArray<byte>))]
    [InlineData(typeof(ImmutableArray<string>))]
    [InlineData(typeof(ImmutableArray<int>))]
    [InlineData(typeof(ImmutableArray<long>))]
    [InlineData(typeof(ImmutableArray<BigInteger>))]
    [InlineData(typeof(ImmutableArray<byte[]>))]
    [InlineData(typeof(ImmutableArray<TestEnum>))]
    [InlineData(typeof(ImmutableArray<DateTimeOffset>))]
    [InlineData(typeof(ImmutableArray<TimeSpan>))]
    public void CanSupportImmutableArray_Test(Type type)
    {
        Assert.True(ModelSerializer.CanSupportType(type));
    }

    [Theory]
    [InlineData(typeof(ImmutableArray<bool?>))]
    [InlineData(typeof(ImmutableArray<byte?>))]
    [InlineData(typeof(ImmutableArray<string?>))]
    [InlineData(typeof(ImmutableArray<int?>))]
    [InlineData(typeof(ImmutableArray<long?>))]
    [InlineData(typeof(ImmutableArray<BigInteger?>))]
    [InlineData(typeof(ImmutableArray<byte[]?>))]
    [InlineData(typeof(ImmutableArray<TestEnum?>))]
    [InlineData(typeof(ImmutableArray<DateTimeOffset?>))]
    [InlineData(typeof(ImmutableArray<TimeSpan?>))]
    public void CanSupportImmutableArray_WithNullableType_Test(Type type)
    {
        Assert.True(ModelSerializer.CanSupportType(type));
    }

    [Theory]
    [InlineData(typeof(ImmutableSortedSet<bool>))]
    [InlineData(typeof(ImmutableSortedSet<byte>))]
    [InlineData(typeof(ImmutableSortedSet<string>))]
    [InlineData(typeof(ImmutableSortedSet<int>))]
    [InlineData(typeof(ImmutableSortedSet<long>))]
    [InlineData(typeof(ImmutableSortedSet<BigInteger>))]
    [InlineData(typeof(ImmutableSortedSet<byte[]>))]
    [InlineData(typeof(ImmutableSortedSet<TestEnum>))]
    [InlineData(typeof(ImmutableSortedSet<DateTimeOffset>))]
    [InlineData(typeof(ImmutableSortedSet<TimeSpan>))]
    public void CanSupportImmutableSortedSet_Test(Type type)
    {
        Assert.True(ModelSerializer.CanSupportType(type));
    }

    [Theory]
    [InlineData(typeof(ImmutableSortedSet<bool?>))]
    [InlineData(typeof(ImmutableSortedSet<byte?>))]
    [InlineData(typeof(ImmutableSortedSet<string?>))]
    [InlineData(typeof(ImmutableSortedSet<int?>))]
    [InlineData(typeof(ImmutableSortedSet<long?>))]
    [InlineData(typeof(ImmutableSortedSet<BigInteger?>))]
    [InlineData(typeof(ImmutableSortedSet<byte[]?>))]
    [InlineData(typeof(ImmutableSortedSet<TestEnum?>))]
    [InlineData(typeof(ImmutableSortedSet<DateTimeOffset?>))]
    [InlineData(typeof(ImmutableSortedSet<TimeSpan?>))]
    public void CanSupportImmutableSortedSet_WithNullableType_Test(Type type)
    {
        Assert.True(ModelSerializer.CanSupportType(type));
    }

    [Theory]
    [InlineData(typeof((int, int)))]
    [InlineData(typeof((int?, int)))]
    public void CanSupportTuple_Test(Type type)
    {
        Assert.True(ModelSerializer.CanSupportType(type));
    }

    [Fact]
    public void CanSupportNonSerializable_FailTest()
    {
        Assert.False(ModelSerializer.CanSupportType(typeof(object)));
    }

    [Theory]
    [InlineData(typeof(sbyte))]
    [InlineData(typeof(short))]
    [InlineData(typeof(ushort))]
    [InlineData(typeof(uint))]
    [InlineData(typeof(ulong))]
    [InlineData(typeof(float))]
    [InlineData(typeof(double))]
    [InlineData(typeof(decimal))]
    [InlineData(typeof(DateTime))]
    [InlineData(typeof(Dictionary<string, string>))]
    [InlineData(typeof(List<string>))]
    [InlineData(typeof(HashSet<string>))]
    [InlineData(typeof(Stack<string>))]
    [InlineData(typeof(Queue<string>))]
    [InlineData(typeof(ImmutableList<string>))]
    [InlineData(typeof(ImmutableHashSet<string>))]
    [InlineData(typeof(ImmutableDictionary<string, string>))]
    [InlineData(typeof(ImmutableQueue<string>))]
    [InlineData(typeof(ImmutableStack<string>))]
    public void CanSupport_FailTest(Type type)
    {
        Assert.False(ModelSerializer.CanSupportType(type));
    }

    [Theory]
    [InlineData(typeof((int, object)))]
    [InlineData(typeof((int?, float)))]
    public void CanSupportTuple_FailTest(Type type)
    {
        Assert.False(ModelSerializer.CanSupportType(type));
    }
}
