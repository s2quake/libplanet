using static Libplanet.TestUtilities.RandomUtility;
using Libplanet.TestUtilities;

namespace Libplanet.Serialization.Tests;

public abstract partial class ModelSerializerTestBase<T>
{
    [Theory]
    [InlineData(0)]
    [InlineData(1074183504)]
    [InlineData(1849913649)]
    [ClassData(typeof(RandomSeedData))]
    public void DictionaryProperty_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedObject = new RecordClassWithDictionary(random);
        var serialized = Serialize(expectedObject);
        var actualObject = Deserialize<RecordClassWithDictionary>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }
}

[Model(Version = 1, TypeName = "Libplanet_Serialization_Tests_ModelSerializerTest_RecordClassWithDictionary")]
public sealed record class RecordClassWithDictionary
    : IEquatable<RecordClassWithDictionary>
{
    public RecordClassWithDictionary()
    {
    }

    public RecordClassWithDictionary(Random random)
    {
        Int32_Int32 = Dictionary(random, Int32, Int32);
        Int32_Int64 = Dictionary(random, Int32, Int64);
        Int32_BigInteger = Dictionary(random, Int32, BigInteger);
        Int32_TestEnum = Dictionary(random, Int32, Enum<TestEnum>);
        Int32_Boolean = Dictionary(random, Int32, Boolean);
        Int32_String = Dictionary(random, Int32, String);
        Int32_DateTimeOffset = Dictionary(random, Int32, DateTimeOffset);
        Int32_TimeSpan = Dictionary(random, Int32, TimeSpan);

        Int64_Int32 = Dictionary(random, Int64, Int32);
        Int64_Int64 = Dictionary(random, Int64, Int64);
        Int64_BigInteger = Dictionary(random, Int64, BigInteger);
        Int64_TestEnum = Dictionary(random, Int64, Enum<TestEnum>);
        Int64_Boolean = Dictionary(random, Int64, Boolean);
        Int64_String = Dictionary(random, Int64, String);
        Int64_DateTimeOffset = Dictionary(random, Int64, DateTimeOffset);
        Int64_TimeSpan = Dictionary(random, Int64, TimeSpan);

        BigInteger_Int32 = Dictionary(random, BigInteger, Int32);
        BigInteger_Int64 = Dictionary(random, BigInteger, Int64);
        BigInteger_BigInteger = Dictionary(random, BigInteger, BigInteger);
        BigInteger_TestEnum = Dictionary(random, BigInteger, Enum<TestEnum>);
        BigInteger_Boolean = Dictionary(random, BigInteger, Boolean);
        BigInteger_String = Dictionary(random, BigInteger, String);
        BigInteger_DateTimeOffset = Dictionary(random, BigInteger, DateTimeOffset);
        BigInteger_TimeSpan = Dictionary(random, BigInteger, TimeSpan);

        TestEnum_Int32 = Dictionary(random, Enum<TestEnum>, Int32);
        TestEnum_Int64 = Dictionary(random, Enum<TestEnum>, Int64);
        TestEnum_BigInteger = Dictionary(random, Enum<TestEnum>, BigInteger);
        TestEnum_TestEnum = Dictionary(random, Enum<TestEnum>, Enum<TestEnum>);
        TestEnum_Boolean = Dictionary(random, Enum<TestEnum>, Boolean);
        TestEnum_String = Dictionary(random, Enum<TestEnum>, String);
        TestEnum_DateTimeOffset = Dictionary(random, Enum<TestEnum>, DateTimeOffset);
        TestEnum_TimeSpan = Dictionary(random, Enum<TestEnum>, TimeSpan);

        Boolean_Int32 = Dictionary(random, Boolean, Int32);
        Boolean_Int64 = Dictionary(random, Boolean, Int64);
        Boolean_BigInteger = Dictionary(random, Boolean, BigInteger);
        Boolean_TestEnum = Dictionary(random, Boolean, Enum<TestEnum>);
        Boolean_Boolean = Dictionary(random, Boolean, Boolean);
        Boolean_String = Dictionary(random, Boolean, String);
        Boolean_DateTimeOffset = Dictionary(random, Boolean, DateTimeOffset);
        Boolean_TimeSpan = Dictionary(random, Boolean, TimeSpan);

        String_Int32 = Dictionary(random, String, Int32);
        String_Int64 = Dictionary(random, String, Int64);
        String_BigInteger = Dictionary(random, String, BigInteger);
        String_TestEnum = Dictionary(random, String, Enum<TestEnum>);
        String_Boolean = Dictionary(random, String, Boolean);
        String_String = Dictionary(random, String, String);
        String_DateTimeOffset = Dictionary(random, String, DateTimeOffset);
        String_TimeSpan = Dictionary(random, String, TimeSpan);

        DateTimeOffset_Int32 = Dictionary(random, DateTimeOffset, Int32);
        DateTimeOffset_Int64 = Dictionary(random, DateTimeOffset, Int64);
        DateTimeOffset_BigInteger = Dictionary(random, DateTimeOffset, BigInteger);
        DateTimeOffset_TestEnum = Dictionary(random, DateTimeOffset, Enum<TestEnum>);
        DateTimeOffset_Boolean = Dictionary(random, DateTimeOffset, Boolean);
        DateTimeOffset_String = Dictionary(random, DateTimeOffset, String);
        DateTimeOffset_DateTimeOffset = Dictionary(random, DateTimeOffset, DateTimeOffset);
        DateTimeOffset_TimeSpan = Dictionary(random, DateTimeOffset, TimeSpan);

        TimeSpan_Int32 = Dictionary(random, TimeSpan, Int32);
        TimeSpan_Int64 = Dictionary(random, TimeSpan, Int64);
        TimeSpan_BigInteger = Dictionary(random, TimeSpan, BigInteger);
        TimeSpan_TestEnum = Dictionary(random, TimeSpan, Enum<TestEnum>);
        TimeSpan_Boolean = Dictionary(random, TimeSpan, Boolean);
        TimeSpan_String = Dictionary(random, TimeSpan, String);
        TimeSpan_DateTimeOffset = Dictionary(random, TimeSpan, DateTimeOffset);
        TimeSpan_TimeSpan = Dictionary(random, TimeSpan, TimeSpan);
    }

    [Property(0)] public Dictionary<int, int> Int32_Int32 { get; init; } = [];
    [Property(1)] public Dictionary<int, long> Int32_Int64 { get; init; } = [];
    [Property(2)] public Dictionary<int, BigInteger> Int32_BigInteger { get; init; } = [];
    [Property(3)] public Dictionary<int, TestEnum> Int32_TestEnum { get; init; } = [];
    [Property(4)] public Dictionary<int, bool> Int32_Boolean { get; init; } = [];
    [Property(5)] public Dictionary<int, string> Int32_String { get; init; } = [];
    [Property(6)] public Dictionary<int, DateTimeOffset> Int32_DateTimeOffset { get; init; } = [];
    [Property(7)] public Dictionary<int, TimeSpan> Int32_TimeSpan { get; init; } = [];

    [Property(8)] public Dictionary<long, int> Int64_Int32 { get; init; } = [];
    [Property(9)] public Dictionary<long, long> Int64_Int64 { get; init; } = [];
    [Property(10)] public Dictionary<long, BigInteger> Int64_BigInteger { get; init; } = [];
    [Property(11)] public Dictionary<long, TestEnum> Int64_TestEnum { get; init; } = [];
    [Property(12)] public Dictionary<long, bool> Int64_Boolean { get; init; } = [];
    [Property(13)] public Dictionary<long, string> Int64_String { get; init; } = [];
    [Property(14)] public Dictionary<long, DateTimeOffset> Int64_DateTimeOffset { get; init; } = [];
    [Property(15)] public Dictionary<long, TimeSpan> Int64_TimeSpan { get; init; } = [];

    [Property(16)] public Dictionary<BigInteger, int> BigInteger_Int32 { get; init; } = [];
    [Property(17)] public Dictionary<BigInteger, long> BigInteger_Int64 { get; init; } = [];
    [Property(18)] public Dictionary<BigInteger, BigInteger> BigInteger_BigInteger { get; init; } = [];
    [Property(19)] public Dictionary<BigInteger, TestEnum> BigInteger_TestEnum { get; init; } = [];
    [Property(20)] public Dictionary<BigInteger, bool> BigInteger_Boolean { get; init; } = [];
    [Property(21)] public Dictionary<BigInteger, string> BigInteger_String { get; init; } = [];
    [Property(22)] public Dictionary<BigInteger, DateTimeOffset> BigInteger_DateTimeOffset { get; init; } = [];
    [Property(23)] public Dictionary<BigInteger, TimeSpan> BigInteger_TimeSpan { get; init; } = [];

    [Property(24)] public Dictionary<TestEnum, int> TestEnum_Int32 { get; init; } = [];
    [Property(25)] public Dictionary<TestEnum, long> TestEnum_Int64 { get; init; } = [];
    [Property(26)] public Dictionary<TestEnum, BigInteger> TestEnum_BigInteger { get; init; } = [];
    [Property(27)] public Dictionary<TestEnum, TestEnum> TestEnum_TestEnum { get; init; } = [];
    [Property(28)] public Dictionary<TestEnum, bool> TestEnum_Boolean { get; init; } = [];
    [Property(29)] public Dictionary<TestEnum, string> TestEnum_String { get; init; } = [];
    [Property(30)] public Dictionary<TestEnum, DateTimeOffset> TestEnum_DateTimeOffset { get; init; } = [];
    [Property(31)] public Dictionary<TestEnum, TimeSpan> TestEnum_TimeSpan { get; init; } = [];

    [Property(32)] public Dictionary<bool, int> Boolean_Int32 { get; init; } = [];
    [Property(33)] public Dictionary<bool, long> Boolean_Int64 { get; init; } = [];
    [Property(34)] public Dictionary<bool, BigInteger> Boolean_BigInteger { get; init; } = [];
    [Property(35)] public Dictionary<bool, TestEnum> Boolean_TestEnum { get; init; } = [];
    [Property(36)] public Dictionary<bool, bool> Boolean_Boolean { get; init; } = [];
    [Property(37)] public Dictionary<bool, string> Boolean_String { get; init; } = [];
    [Property(38)] public Dictionary<bool, DateTimeOffset> Boolean_DateTimeOffset { get; init; } = [];
    [Property(39)] public Dictionary<bool, TimeSpan> Boolean_TimeSpan { get; init; } = [];

    [Property(40)] public Dictionary<string, int> String_Int32 { get; init; } = [];
    [Property(41)] public Dictionary<string, long> String_Int64 { get; init; } = [];
    [Property(42)] public Dictionary<string, BigInteger> String_BigInteger { get; init; } = [];
    [Property(43)] public Dictionary<string, TestEnum> String_TestEnum { get; init; } = [];
    [Property(44)] public Dictionary<string, bool> String_Boolean { get; init; } = [];
    [Property(45)] public Dictionary<string, string> String_String { get; init; } = [];
    [Property(46)] public Dictionary<string, DateTimeOffset> String_DateTimeOffset { get; init; } = [];
    [Property(47)] public Dictionary<string, TimeSpan> String_TimeSpan { get; init; } = [];

    [Property(48)] public Dictionary<DateTimeOffset, int> DateTimeOffset_Int32 { get; init; } = [];
    [Property(49)] public Dictionary<DateTimeOffset, long> DateTimeOffset_Int64 { get; init; } = [];
    [Property(50)] public Dictionary<DateTimeOffset, BigInteger> DateTimeOffset_BigInteger { get; init; } = [];
    [Property(51)] public Dictionary<DateTimeOffset, TestEnum> DateTimeOffset_TestEnum { get; init; } = [];
    [Property(52)] public Dictionary<DateTimeOffset, bool> DateTimeOffset_Boolean { get; init; } = [];
    [Property(53)] public Dictionary<DateTimeOffset, string> DateTimeOffset_String { get; init; } = [];
    [Property(54)] public Dictionary<DateTimeOffset, DateTimeOffset> DateTimeOffset_DateTimeOffset { get; init; } = [];
    [Property(55)] public Dictionary<DateTimeOffset, TimeSpan> DateTimeOffset_TimeSpan { get; init; } = [];

    [Property(56)] public Dictionary<TimeSpan, int> TimeSpan_Int32 { get; init; } = [];
    [Property(57)] public Dictionary<TimeSpan, long> TimeSpan_Int64 { get; init; } = [];
    [Property(58)] public Dictionary<TimeSpan, BigInteger> TimeSpan_BigInteger { get; init; } = [];
    [Property(59)] public Dictionary<TimeSpan, TestEnum> TimeSpan_TestEnum { get; init; } = [];
    [Property(60)] public Dictionary<TimeSpan, bool> TimeSpan_Boolean { get; init; } = [];
    [Property(61)] public Dictionary<TimeSpan, string> TimeSpan_String { get; init; } = [];
    [Property(62)] public Dictionary<TimeSpan, DateTimeOffset> TimeSpan_DateTimeOffset { get; init; } = [];
    [Property(63)] public Dictionary<TimeSpan, TimeSpan> TimeSpan_TimeSpan { get; init; } = [];

    public bool Equals(RecordClassWithDictionary? other) => ModelResolver.Equals(this, other);

    public override int GetHashCode() => ModelResolver.GetHashCode(this);
}
