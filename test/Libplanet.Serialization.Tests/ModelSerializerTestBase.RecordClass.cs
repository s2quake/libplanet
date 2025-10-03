using Libplanet.TestUtilities;
using static Libplanet.TestUtilities.RandomUtility;

namespace Libplanet.Serialization.Tests;

public abstract partial class ModelSerializerTestBase<TData>
{
    [Fact]
    public void ObjectRecordClass_SerializeAndDeserialize_Test()
    {
        var expectedObject = new ObjectRecordClass();
        var serialized = Serialize(expectedObject);
        var actualObject = Deserialize<ObjectRecordClass>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1074183504)]
    [ClassData(typeof(RandomSeedsData))]
    public void ObjectRecordClass_SerializeAndDeserialize_Seed_Test(int seed)
    {
        var random = new Random(seed);
        var expectedObject = new ObjectRecordClass(random);
        var serialized = Serialize(expectedObject);
        var actualObject = Deserialize<ObjectRecordClass>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }

    [Fact]
    public void ArrayRecordClass_SerializeAndDeserialize_Test()
    {
        var expectedObject = new ArrayRecordClass();
        var serialized = Serialize(expectedObject);
        var actualObject = Deserialize<ArrayRecordClass>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1074183504)]
    [ClassData(typeof(RandomSeedsData))]
    public void ArrayRecordClass_SerializeAndDeserialize_Seed_Test(int seed)
    {
        var random = new Random(seed);
        var expectedObject = new ArrayRecordClass(random);
        var serialized = Serialize(expectedObject);
        var actualObject = Deserialize<ArrayRecordClass>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }

    [Fact]
    public void MixedRecordClass_SerializeAndDeserialize_Test()
    {
        var expectedObject = new MixedRecordClass();
        var serialized = Serialize(expectedObject);
        var actualObject = Deserialize<MixedRecordClass>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1074183504)]
    [ClassData(typeof(RandomSeedsData))]
    public void MixedRecordClass_SerializeAndDeserialize_Seed_Test(int seed)
    {
        var random = new Random(seed);
        var expectedObject = new MixedRecordClass(random);
        var serialized = Serialize(expectedObject);
        var actualObject = Deserialize<MixedRecordClass>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }
}

[Model("Libplanet_Serialization_Tests_ModelSerializerTest_ObjectRecordClass", Version = 1)]
public sealed record class ObjectRecordClass : IEquatable<ObjectRecordClass>
{
    public ObjectRecordClass()
    {
    }

    public ObjectRecordClass(Random random)
    {
        Int = Int32(random);
        Long = Int64(random);
        BigInteger = BigInteger(random);
        Enum = Enum<TestEnum>(random);
        Bool = Boolean(random);
        String = String(random);
        DateTimeOffset = DateTimeOffset(random);
        TimeSpan = TimeSpan(random);
        Byte = Byte(random);
    }

    [Property(0)]
    public int Int { get; init; }

    [Property(1)]
    public long Long { get; init; }

    [Property(2)]
    public BigInteger BigInteger { get; init; }

    [Property(3)]
    public TestEnum Enum { get; init; }

    [Property(4)]
    public bool Bool { get; init; }

    [Property(5)]
    public string String { get; init; } = string.Empty;

    [Property(6)]
    public DateTimeOffset DateTimeOffset { get; init; } = DateTimeOffset.UnixEpoch;

    [Property(7)]
    public TimeSpan TimeSpan { get; init; }

    [Property(8)]
    public byte Byte { get; init; }

    public bool Equals(ObjectRecordClass? other) => ModelResolver.Equals(this, other);

    public override int GetHashCode() => ModelResolver.GetHashCode(this);
}

[Model("Libplanet_Serialization_Tests_ModelSerializerTest_ArrayRecordClass", Version = 1)]
public sealed record class ArrayRecordClass : IEquatable<ArrayRecordClass>
{
    public ArrayRecordClass()
    {
    }

    public ArrayRecordClass(Random random)
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
    public int[] Ints { get; init; } = [];

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

    public bool Equals(ArrayRecordClass? other) => ModelResolver.Equals(this, other);

    public override int GetHashCode() => ModelResolver.GetHashCode(this);
}

[Model("Libplanet_Serialization_Tests_ModelSerializerTest_MixedRecordClass", Version = 1)]
public sealed record class MixedRecordClass : IEquatable<MixedRecordClass>
{
    public MixedRecordClass()
    {
        Object = new ObjectRecordClass();
    }

    public MixedRecordClass(Random random)
    {
        Object = new ObjectRecordClass(random);
        Objects = Array(random, random => new ObjectRecordClass(random));
    }

    [Property(0)]
    public ObjectRecordClass Object { get; init; }

    [Property(1)]
    public ObjectRecordClass[] Objects { get; init; } = [];

    public bool Equals(MixedRecordClass? other) => ModelResolver.Equals(this, other);

    public override int GetHashCode() => ModelResolver.GetHashCode(this);
}
