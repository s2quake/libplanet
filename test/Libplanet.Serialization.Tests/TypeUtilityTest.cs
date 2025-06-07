namespace Libplanet.Serialization.Tests;

public sealed class TypeUtilityTest
{
    [Theory]
    [InlineData(typeof(object), "o")]
    [InlineData(typeof(BigInteger), "bi")]
    [InlineData(typeof(BigInteger?), "bi?")]
    [InlineData(typeof(bool), "b")]
    [InlineData(typeof(bool?), "b?")]
    [InlineData(typeof(byte), "y")]
    [InlineData(typeof(byte?), "y?")]
    [InlineData(typeof(char), "c")]
    [InlineData(typeof(char?), "c?")]
    [InlineData(typeof(DateTimeOffset), "dt")]
    [InlineData(typeof(DateTimeOffset?), "dt?")]
    [InlineData(typeof(Guid), "id")]
    [InlineData(typeof(Guid?), "id?")]
    [InlineData(typeof(int), "i")]
    [InlineData(typeof(int?), "i?")]
    [InlineData(typeof(long), "l")]
    [InlineData(typeof(long?), "l?")]
    [InlineData(typeof(string), "s")]
    [InlineData(typeof(TimeSpan), "ts")]
    [InlineData(typeof(TimeSpan?), "ts?")]
    [InlineData(typeof(Array), "ar")]
    [InlineData(typeof(ImmutableArray<int>), "imar<i>")]
    [InlineData(typeof(ImmutableArray<int?>), "imar<i?>")]
    [InlineData(typeof(ImmutableArray<int>?), "imar<i>?")]
    [InlineData(typeof(ImmutableArray<int?>?), "imar<i?>?")]
    [InlineData(typeof(ImmutableList<int>), "imli<i>")]
    [InlineData(typeof(ImmutableList<int?>), "imli<i?>")]
    [InlineData(typeof(ImmutableSortedSet<int>), "imss<i>")]
    [InlineData(typeof(ImmutableSortedSet<int?>), "imss<i?>")]
    [InlineData(typeof(ImmutableDictionary<int, string>), "imdi<i,s>")]
    [InlineData(typeof(ImmutableSortedDictionary<int, string>), "imsd<i,s>")]
    public void GetTypeName(Type type, string expectedName)
    {
        var actualName = TypeUtility.GetTypeName(type);
        Assert.Equal(expectedName, actualName);
    }

    [Theory]
    [InlineData("o", typeof(object))]
    [InlineData("bi", typeof(BigInteger))]
    [InlineData("bi?", typeof(BigInteger?))]
    [InlineData("b", typeof(bool))]
    [InlineData("b?", typeof(bool?))]
    [InlineData("y", typeof(byte))]
    [InlineData("y?", typeof(byte?))]
    [InlineData("c", typeof(char))]
    [InlineData("c?", typeof(char?))]
    [InlineData("dt", typeof(DateTimeOffset))]
    [InlineData("dt?", typeof(DateTimeOffset?))]
    [InlineData("id", typeof(Guid))]
    [InlineData("id?", typeof(Guid?))]
    [InlineData("i", typeof(int))]
    [InlineData("i?", typeof(int?))]
    [InlineData("l", typeof(long))]
    [InlineData("l?", typeof(long?))]
    [InlineData("s", typeof(string))]
    [InlineData("ts", typeof(TimeSpan))]
    [InlineData("ts?", typeof(TimeSpan?))]
    [InlineData("ar", typeof(Array))]
    [InlineData("imar<i>", typeof(ImmutableArray<int>))]
    [InlineData("imar<i?>", typeof(ImmutableArray<int?>))]
    [InlineData("imar<i>?", typeof(ImmutableArray<int>?))]
    [InlineData("imar<i?>?", typeof(ImmutableArray<int?>?))]
    [InlineData("imli<i>", typeof(ImmutableList<int>))]
    [InlineData("imli<i?>", typeof(ImmutableList<int?>))]
    [InlineData("imss<i>", typeof(ImmutableSortedSet<int>))]
    [InlineData("imss<i?>", typeof(ImmutableSortedSet<int?>))]
    [InlineData("imdi<i,s>", typeof(ImmutableDictionary<int, string>))]
    [InlineData("imsd<i,s>", typeof(ImmutableSortedDictionary<int, string>))]
    public void GetType_Test(string typeName, Type expectedType)
    {
        var actualType = TypeUtility.GetType(typeName);
        Assert.Equal(expectedType, actualType);
    }
}
