using static Libplanet.TestUtilities.RandomUtility;
using Libplanet.TestUtilities;

namespace Libplanet.Serialization.Tests;

public abstract partial class ModelSerializerTestBase<TData>
{
    [Theory]
    [InlineData(0)]
    [InlineData(1074183504)]
    [InlineData(1849913649)]
    [ClassData(typeof(RandomSeedsData))]
    public void SortedDictionaryProperty_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedObject = new RecordClassWithSortedDictionary(random);
        var serialized = Serialize(expectedObject);
        var actualObject = Deserialize<RecordClassWithSortedDictionary>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }

    [Theory]
    [InlineData(0)]
    [ClassData(typeof(RandomSeedsData))]
    public void SortedDictionary_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedObject = new RecordClassWithSortedDictionary(random);
        var properties = ModelResolver.GetProperties(expectedObject.GetType());
        foreach (var property in properties)
        {
            var expectedValue = property.GetValue(expectedObject);
            var serialized1 = Serialize(expectedValue);
            var actualValue1 = Deserialize(serialized1);
            Assert.Equal(expectedValue, actualValue1);

            var options = new ModelOptions { TypeInfoMode = ModelTypeInfoMode.Never };
            var serialized2 = Serialize(expectedValue, options);
            var actualValue2 = Deserialize(serialized2, property.PropertyType, options);
            Assert.Equal(expectedValue, actualValue2);

            var serialized3 = Serialize(expectedValue, options);
            Assert.ThrowsAny<ModelException>(() => Deserialize(serialized3, options));
        }
    }
}

[Model("Libplanet_Serialization_Tests_ModelSerializerTest_RecordClassWithSortedDictionary", Version = 1)]
public sealed record class RecordClassWithSortedDictionary
    : IEquatable<RecordClassWithSortedDictionary>
{
    public RecordClassWithSortedDictionary()
    {
    }

    public RecordClassWithSortedDictionary(Random random)
    {
        Int32_Int32 = SortedDictionary(random, Int32, Int32);
        Int32_Int64 = SortedDictionary(random, Int32, Int64);
        Int32_BigInteger = SortedDictionary(random, Int32, BigInteger);
        Int32_TestEnum = SortedDictionary(random, Int32, Enum<TestEnum>);
        Int32_Boolean = SortedDictionary(random, Int32, Boolean);
        Int32_String = SortedDictionary(random, Int32, String);
        Int32_DateTimeOffset = SortedDictionary(random, Int32, DateTimeOffset);
        Int32_TimeSpan = SortedDictionary(random, Int32, TimeSpan);

        Int64_Int32 = SortedDictionary(random, Int64, Int32);
        Int64_Int64 = SortedDictionary(random, Int64, Int64);
        Int64_BigInteger = SortedDictionary(random, Int64, BigInteger);
        Int64_TestEnum = SortedDictionary(random, Int64, Enum<TestEnum>);
        Int64_Boolean = SortedDictionary(random, Int64, Boolean);
        Int64_String = SortedDictionary(random, Int64, String);
        Int64_DateTimeOffset = SortedDictionary(random, Int64, DateTimeOffset);
        Int64_TimeSpan = SortedDictionary(random, Int64, TimeSpan);

        BigInteger_Int32 = SortedDictionary(random, BigInteger, Int32);
        BigInteger_Int64 = SortedDictionary(random, BigInteger, Int64);
        BigInteger_BigInteger = SortedDictionary(random, BigInteger, BigInteger);
        BigInteger_TestEnum = SortedDictionary(random, BigInteger, Enum<TestEnum>);
        BigInteger_Boolean = SortedDictionary(random, BigInteger, Boolean);
        BigInteger_String = SortedDictionary(random, BigInteger, String);
        BigInteger_DateTimeOffset = SortedDictionary(random, BigInteger, DateTimeOffset);
        BigInteger_TimeSpan = SortedDictionary(random, BigInteger, TimeSpan);

        TestEnum_Int32 = SortedDictionary(random, Enum<TestEnum>, Int32);
        TestEnum_Int64 = SortedDictionary(random, Enum<TestEnum>, Int64);
        TestEnum_BigInteger = SortedDictionary(random, Enum<TestEnum>, BigInteger);
        TestEnum_TestEnum = SortedDictionary(random, Enum<TestEnum>, Enum<TestEnum>);
        TestEnum_Boolean = SortedDictionary(random, Enum<TestEnum>, Boolean);
        TestEnum_String = SortedDictionary(random, Enum<TestEnum>, String);
        TestEnum_DateTimeOffset = SortedDictionary(random, Enum<TestEnum>, DateTimeOffset);
        TestEnum_TimeSpan = SortedDictionary(random, Enum<TestEnum>, TimeSpan);

        Boolean_Int32 = SortedDictionary(random, Boolean, Int32);
        Boolean_Int64 = SortedDictionary(random, Boolean, Int64);
        Boolean_BigInteger = SortedDictionary(random, Boolean, BigInteger);
        Boolean_TestEnum = SortedDictionary(random, Boolean, Enum<TestEnum>);
        Boolean_Boolean = SortedDictionary(random, Boolean, Boolean);
        Boolean_String = SortedDictionary(random, Boolean, String);
        Boolean_DateTimeOffset = SortedDictionary(random, Boolean, DateTimeOffset);
        Boolean_TimeSpan = SortedDictionary(random, Boolean, TimeSpan);

        String_Int32 = SortedDictionary(random, String, Int32);
        String_Int64 = SortedDictionary(random, String, Int64);
        String_BigInteger = SortedDictionary(random, String, BigInteger);
        String_TestEnum = SortedDictionary(random, String, Enum<TestEnum>);
        String_Boolean = SortedDictionary(random, String, Boolean);
        String_String = SortedDictionary(random, String, String);
        String_DateTimeOffset = SortedDictionary(random, String, DateTimeOffset);
        String_TimeSpan = SortedDictionary(random, String, TimeSpan);

        DateTimeOffset_Int32 = SortedDictionary(random, DateTimeOffset, Int32);
        DateTimeOffset_Int64 = SortedDictionary(random, DateTimeOffset, Int64);
        DateTimeOffset_BigInteger = SortedDictionary(random, DateTimeOffset, BigInteger);
        DateTimeOffset_TestEnum = SortedDictionary(random, DateTimeOffset, Enum<TestEnum>);
        DateTimeOffset_Boolean = SortedDictionary(random, DateTimeOffset, Boolean);
        DateTimeOffset_String = SortedDictionary(random, DateTimeOffset, String);
        DateTimeOffset_DateTimeOffset = SortedDictionary(random, DateTimeOffset, DateTimeOffset);
        DateTimeOffset_TimeSpan = SortedDictionary(random, DateTimeOffset, TimeSpan);

        TimeSpan_Int32 = SortedDictionary(random, TimeSpan, Int32);
        TimeSpan_Int64 = SortedDictionary(random, TimeSpan, Int64);
        TimeSpan_BigInteger = SortedDictionary(random, TimeSpan, BigInteger);
        TimeSpan_TestEnum = SortedDictionary(random, TimeSpan, Enum<TestEnum>);
        TimeSpan_Boolean = SortedDictionary(random, TimeSpan, Boolean);
        TimeSpan_String = SortedDictionary(random, TimeSpan, String);
        TimeSpan_DateTimeOffset = SortedDictionary(random, TimeSpan, DateTimeOffset);
        TimeSpan_TimeSpan = SortedDictionary(random, TimeSpan, TimeSpan);
    }

    [Property(0)]  public SortedDictionary<int, int> Int32_Int32 { get; init; } = [];
    [Property(1)]  public SortedDictionary<int, long> Int32_Int64 { get; init; } = [];
    [Property(2)]  public SortedDictionary<int, BigInteger> Int32_BigInteger { get; init; } = [];
    [Property(3)]  public SortedDictionary<int, TestEnum> Int32_TestEnum { get; init; } = [];
    [Property(4)]  public SortedDictionary<int, bool> Int32_Boolean { get; init; } = [];
    [Property(5)]  public SortedDictionary<int, string> Int32_String { get; init; } = [];
    [Property(6)]  public SortedDictionary<int, DateTimeOffset> Int32_DateTimeOffset { get; init; } = [];
    [Property(7)]  public SortedDictionary<int, TimeSpan> Int32_TimeSpan { get; init; } = [];

    [Property(8)]  public SortedDictionary<long, int> Int64_Int32 { get; init; } = [];
    [Property(9)]  public SortedDictionary<long, long> Int64_Int64 { get; init; } = [];
    [Property(10)] public SortedDictionary<long, BigInteger> Int64_BigInteger { get; init; } = [];
    [Property(11)] public SortedDictionary<long, TestEnum> Int64_TestEnum { get; init; } = [];
    [Property(12)] public SortedDictionary<long, bool> Int64_Boolean { get; init; } = [];
    [Property(13)] public SortedDictionary<long, string> Int64_String { get; init; } = [];
    [Property(14)] public SortedDictionary<long, DateTimeOffset> Int64_DateTimeOffset { get; init; } = [];
    [Property(15)] public SortedDictionary<long, TimeSpan> Int64_TimeSpan { get; init; } = [];

    [Property(16)] public SortedDictionary<BigInteger, int> BigInteger_Int32 { get; init; } = [];
    [Property(17)] public SortedDictionary<BigInteger, long> BigInteger_Int64 { get; init; } = [];
    [Property(18)] public SortedDictionary<BigInteger, BigInteger> BigInteger_BigInteger { get; init; } = [];
    [Property(19)] public SortedDictionary<BigInteger, TestEnum> BigInteger_TestEnum { get; init; } = [];
    [Property(20)] public SortedDictionary<BigInteger, bool> BigInteger_Boolean { get; init; } = [];
    [Property(21)] public SortedDictionary<BigInteger, string> BigInteger_String { get; init; } = [];
    [Property(22)] public SortedDictionary<BigInteger, DateTimeOffset> BigInteger_DateTimeOffset { get; init; } = [];
    [Property(23)] public SortedDictionary<BigInteger, TimeSpan> BigInteger_TimeSpan { get; init; } = [];

    [Property(24)] public SortedDictionary<TestEnum, int> TestEnum_Int32 { get; init; } = [];
    [Property(25)] public SortedDictionary<TestEnum, long> TestEnum_Int64 { get; init; } = [];
    [Property(26)] public SortedDictionary<TestEnum, BigInteger> TestEnum_BigInteger { get; init; } = [];
    [Property(27)] public SortedDictionary<TestEnum, TestEnum> TestEnum_TestEnum { get; init; } = [];
    [Property(28)] public SortedDictionary<TestEnum, bool> TestEnum_Boolean { get; init; } = [];
    [Property(29)] public SortedDictionary<TestEnum, string> TestEnum_String { get; init; } = [];
    [Property(30)] public SortedDictionary<TestEnum, DateTimeOffset> TestEnum_DateTimeOffset { get; init; } = [];
    [Property(31)] public SortedDictionary<TestEnum, TimeSpan> TestEnum_TimeSpan { get; init; } = [];

    [Property(32)] public SortedDictionary<bool, int> Boolean_Int32 { get; init; } = [];
    [Property(33)] public SortedDictionary<bool, long> Boolean_Int64 { get; init; } = [];
    [Property(34)] public SortedDictionary<bool, BigInteger> Boolean_BigInteger { get; init; } = [];
    [Property(35)] public SortedDictionary<bool, TestEnum> Boolean_TestEnum { get; init; } = [];
    [Property(36)] public SortedDictionary<bool, bool> Boolean_Boolean { get; init; } = [];
    [Property(37)] public SortedDictionary<bool, string> Boolean_String { get; init; } = [];
    [Property(38)] public SortedDictionary<bool, DateTimeOffset> Boolean_DateTimeOffset { get; init; } = [];
    [Property(39)] public SortedDictionary<bool, TimeSpan> Boolean_TimeSpan { get; init; } = [];

    [Property(40)] public SortedDictionary<string, int> String_Int32 { get; init; } = [];
    [Property(41)] public SortedDictionary<string, long> String_Int64 { get; init; } = [];
    [Property(42)] public SortedDictionary<string, BigInteger> String_BigInteger { get; init; } = [];
    [Property(43)] public SortedDictionary<string, TestEnum> String_TestEnum { get; init; } = [];
    [Property(44)] public SortedDictionary<string, bool> String_Boolean { get; init; } = [];
    [Property(45)] public SortedDictionary<string, string> String_String { get; init; } = [];
    [Property(46)] public SortedDictionary<string, DateTimeOffset> String_DateTimeOffset { get; init; } = [];
    [Property(47)] public SortedDictionary<string, TimeSpan> String_TimeSpan { get; init; } = [];

    [Property(48)] public SortedDictionary<DateTimeOffset, int> DateTimeOffset_Int32 { get; init; } = [];
    [Property(49)] public SortedDictionary<DateTimeOffset, long> DateTimeOffset_Int64 { get; init; } = [];
    [Property(50)] public SortedDictionary<DateTimeOffset, BigInteger> DateTimeOffset_BigInteger { get; init; } = [];
    [Property(51)] public SortedDictionary<DateTimeOffset, TestEnum> DateTimeOffset_TestEnum { get; init; } = [];
    [Property(52)] public SortedDictionary<DateTimeOffset, bool> DateTimeOffset_Boolean { get; init; } = [];
    [Property(53)] public SortedDictionary<DateTimeOffset, string> DateTimeOffset_String { get; init; } = [];
    [Property(54)] public SortedDictionary<DateTimeOffset, DateTimeOffset> DateTimeOffset_DateTimeOffset { get; init; } = [];
    [Property(55)] public SortedDictionary<DateTimeOffset, TimeSpan> DateTimeOffset_TimeSpan { get; init; } = [];

    [Property(56)] public SortedDictionary<TimeSpan, int> TimeSpan_Int32 { get; init; } = [];
    [Property(57)] public SortedDictionary<TimeSpan, long> TimeSpan_Int64 { get; init; } = [];
    [Property(58)] public SortedDictionary<TimeSpan, BigInteger> TimeSpan_BigInteger { get; init; } = [];
    [Property(59)] public SortedDictionary<TimeSpan, TestEnum> TimeSpan_TestEnum { get; init; } = [];
    [Property(60)] public SortedDictionary<TimeSpan, bool> TimeSpan_Boolean { get; init; } = [];
    [Property(61)] public SortedDictionary<TimeSpan, string> TimeSpan_String { get; init; } = [];
    [Property(62)] public SortedDictionary<TimeSpan, DateTimeOffset> TimeSpan_DateTimeOffset { get; init; } = [];
    [Property(63)] public SortedDictionary<TimeSpan, TimeSpan> TimeSpan_TimeSpan { get; init; } = [];

    public bool Equals(RecordClassWithSortedDictionary? other) => ModelResolver.Equals(this, other);

    public override int GetHashCode() => ModelResolver.GetHashCode(this);
}
