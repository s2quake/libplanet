using static Libplanet.Tests.RandomUtility;

namespace Libplanet.Serialization.Tests;

public sealed partial class ModelSerializerTest
{
    [Fact]
    public void ObjectRecordStruct_SerializeAndDeserialize_Test()
    {
        var expectedObject = new ObjectRecordStruct();
        var serialized = ModelSerializer.Serialize(expectedObject);
        var actualObject = ModelSerializer.Deserialize<ObjectRecordStruct>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1074183504)]
    [MemberData(nameof(RandomSeeds))]
    public void ObjectRecordStruct_SerializeAndDeserialize_Seed_Test(int seed)
    {
        var random = new Random(seed);
        var expectedObject = new ObjectRecordStruct(random);
        var serialized = ModelSerializer.Serialize(expectedObject);
        var actualObject = ModelSerializer.Deserialize<ObjectRecordStruct>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }

    [Fact]
    public void ArrayRecordStruct_SerializeAndDeserialize_Test()
    {
        var expectedObject = new ArrayRecordStruct();
        var serialized = ModelSerializer.Serialize(expectedObject);
        var actualObject = ModelSerializer.Deserialize<ArrayRecordStruct>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1074183504)]
    [MemberData(nameof(RandomSeeds))]
    public void ArrayRecordStruct_SerializeAndDeserialize_Seed_Test(int seed)
    {
        var random = new Random(seed);
        var expectedObject = new ArrayRecordStruct(random);
        var serialized = ModelSerializer.Serialize(expectedObject);
        var actualObject = ModelSerializer.Deserialize<ArrayRecordStruct>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }

    [Fact]
    public void MixedRecordStruct_SerializeAndDeserialize_Test()
    {
        var expectedObject = new MixedRecordStruct();
        var serialized = ModelSerializer.Serialize(expectedObject);
        var actualObject = ModelSerializer.Deserialize<MixedRecordStruct>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1074183504)]
    [MemberData(nameof(RandomSeeds))]
    public void MixedRecordStruct_SerializeAndDeserialize_Seed_Test(int seed)
    {
        var random = new Random(seed);
        var expectedObject = new MixedRecordStruct(random);
        var serialized = ModelSerializer.Serialize(expectedObject);
        var actualObject = ModelSerializer.Deserialize<MixedRecordStruct>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }

    [Model(Version = 1)]
    public readonly record struct ObjectRecordStruct : IEquatable<ObjectRecordStruct>
    {
        public ObjectRecordStruct()
        {
        }

        public ObjectRecordStruct(Random random)
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

        public bool Equals(ObjectRecordStruct other) => ModelResolver.Equals(this, other);

        public override int GetHashCode() => ModelResolver.GetHashCode(this);
    }

    [Model(Version = 1)]
    public readonly record struct ArrayRecordStruct : IEquatable<ArrayRecordStruct>
    {
        public ArrayRecordStruct()
        {
        }

        public ArrayRecordStruct(Random random)
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

        public bool Equals(ArrayRecordStruct other) => ModelResolver.Equals(this, other);

        public override int GetHashCode() => ModelResolver.GetHashCode(this);
    }

    [Model(Version = 1)]
    public readonly record struct MixedRecordStruct : IEquatable<MixedRecordStruct>
    {
        public MixedRecordStruct()
        {
            Object = new ObjectRecordStruct();
        }

        public MixedRecordStruct(Random random)
        {
            Object = new ObjectRecordStruct(random);
            Objects = Array(random, random => new ObjectRecordStruct(random));
        }

        [Property(0)]
        public ObjectRecordStruct Object { get; init; }

        [Property(1)]
        public ObjectRecordStruct[] Objects { get; init; } = [];

        public bool Equals(MixedRecordStruct other) => ModelResolver.Equals(this, other);

        public override int GetHashCode() => ModelResolver.GetHashCode(this);
    }
}
