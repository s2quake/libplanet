using static Libplanet.Tests.RandomUtility;

namespace Libplanet.Serialization.Tests;

public sealed partial class SerializerTest
{
    [Fact]
    public void ObjectClass_SerializeAndDeserialize_Test()
    {
        var expectedObject = new ObjectClass();
        var serialized = ModelSerializer.Serialize(expectedObject);
        var actualObject = ModelSerializer.Deserialize<ObjectClass>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1074183504)]
    [MemberData(nameof(RandomSeeds))]
    public void ObjectClass_SerializeAndDeserialize_Seed_Test(int seed)
    {
        var random = new Random(seed);
        var expectedObject = new ObjectClass(random);
        var serialized = ModelSerializer.Serialize(expectedObject);
        var actualObject = ModelSerializer.Deserialize<ObjectClass>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }

    [Fact]
    public void ArrayClass_SerializeAndDeserialize_Test()
    {
        var expectedObject = new ArrayClass();
        var serialized = ModelSerializer.Serialize(expectedObject);
        var actualObject = ModelSerializer.Deserialize<ArrayClass>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1074183504)]
    [MemberData(nameof(RandomSeeds))]
    public void ArrayClass_SerializeAndDeserialize_Seed_Test(int seed)
    {
        var random = new Random(seed);
        var expectedObject = new ArrayClass(random);
        var serialized = ModelSerializer.Serialize(expectedObject);
        var actualObject = ModelSerializer.Deserialize<ArrayClass>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }

    [Fact]
    public void MixedClass_SerializeAndDeserialize_Test()
    {
        var expectedObject = new MixedClass();
        var serialized = ModelSerializer.Serialize(expectedObject);
        var actualObject = ModelSerializer.Deserialize<MixedClass>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1074183504)]
    [MemberData(nameof(RandomSeeds))]
    public void MixedClass_SerializeAndDeserialize_Seed_Test(int seed)
    {
        var random = new Random(seed);
        var expectedObject = new MixedClass(random);
        var serialized = ModelSerializer.Serialize(expectedObject);
        var actualObject = ModelSerializer.Deserialize<MixedClass>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }

    [Model(Version = 1)]
    public sealed class ObjectClass : IEquatable<ObjectClass>
    {
        public ObjectClass()
        {
        }

        public ObjectClass(Random random)
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

        public bool Equals(ObjectClass? other) => ModelUtility.Equals(this, other);

        public override bool Equals(object? obj) => Equals(obj as ObjectClass);

        public override int GetHashCode() => ModelUtility.GetHashCode(this);
    }

    [Model(Version = 1)]
    public sealed class ArrayClass : IEquatable<ArrayClass>
    {
        public ArrayClass()
        {
        }

        public ArrayClass(Random random)
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

        public bool Equals(ArrayClass? other) => ModelUtility.Equals(this, other);

        public override bool Equals(object? obj) => Equals(obj as ArrayClass);

        public override int GetHashCode() => ModelUtility.GetHashCode(this);
    }

    [Model(Version = 1)]
    public sealed class MixedClass : IEquatable<MixedClass>
    {
        public MixedClass()
        {
            Object = new ObjectClass();
        }

        public MixedClass(Random random)
        {
            Object = new ObjectClass(random);
            Objects = Array(random, random => new ObjectClass(random));
        }

        [Property(0)]
        public ObjectClass Object { get; init; }

        [Property(1)]
        public ObjectClass[] Objects { get; init; } = [];

        public bool Equals(MixedClass? other) => ModelUtility.Equals(this, other);

        public override bool Equals(object? obj) => Equals(obj as MixedClass);

        public override int GetHashCode() => ModelUtility.GetHashCode(this);
    }
}
