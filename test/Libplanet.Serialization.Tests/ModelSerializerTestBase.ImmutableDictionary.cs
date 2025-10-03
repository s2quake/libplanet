using Libplanet.TestUtilities;
using static Libplanet.TestUtilities.RandomUtility;

namespace Libplanet.Serialization.Tests;

public abstract partial class ModelSerializerTestBase<TData>
{
    [Theory]
    [InlineData(0)]
    [InlineData(1074183504)]
    [InlineData(1849913649)]
    [ClassData(typeof(RandomSeedsData))]
    public void ImmutableDictionaryProperty_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedObject = new RecordClassWithImmutableDictionary(random);
        var serialized = Serialize(expectedObject);
        var actualObject = Deserialize<RecordClassWithImmutableDictionary>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }

    [Theory]
    [InlineData(0)]
    [ClassData(typeof(RandomSeedsData))]
    public void ImmutableDictionary_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedObject = new RecordClassWithImmutableDictionary(random);
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

[Model("Libplanet_Serialization_Tests_ModelSerializerTest_RecordClassWithImmutableDictionary", Version = 1)]
public sealed record class RecordClassWithImmutableDictionary
    : IEquatable<RecordClassWithImmutableDictionary>
{
    public RecordClassWithImmutableDictionary()
    {
    }

    public RecordClassWithImmutableDictionary(Random random)
    {
        // int -> *
        Value1 = ImmutableDictionary(random, Int32, Int32);
        Value2 = ImmutableDictionary(random, Int32, Int64);
        Value3 = ImmutableDictionary(random, Int32, BigInteger);
        Value4 = ImmutableDictionary(random, Int32, Enum<TestEnum>);
        Value5 = ImmutableDictionary(random, Int32, Boolean);
        Value6 = ImmutableDictionary(random, Int32, String);
        Value7 = ImmutableDictionary(random, Int32, DateTimeOffset);
        Value8 = ImmutableDictionary(random, Int32, TimeSpan);

        // long -> *
        Value9 = ImmutableDictionary(random, Int64, Int32);
        Value10 = ImmutableDictionary(random, Int64, Int64);
        Value11 = ImmutableDictionary(random, Int64, BigInteger);
        Value12 = ImmutableDictionary(random, Int64, Enum<TestEnum>);
        Value13 = ImmutableDictionary(random, Int64, Boolean);
        Value14 = ImmutableDictionary(random, Int64, String);
        Value15 = ImmutableDictionary(random, Int64, DateTimeOffset);
        Value16 = ImmutableDictionary(random, Int64, TimeSpan);

        // BigInteger -> *
        Value17 = ImmutableDictionary(random, BigInteger, Int32);
        Value18 = ImmutableDictionary(random, BigInteger, Int64);
        Value19 = ImmutableDictionary(random, BigInteger, BigInteger);
        Value20 = ImmutableDictionary(random, BigInteger, Enum<TestEnum>);
        Value21 = ImmutableDictionary(random, BigInteger, Boolean);
        Value22 = ImmutableDictionary(random, BigInteger, String);
        Value23 = ImmutableDictionary(random, BigInteger, DateTimeOffset);
        Value24 = ImmutableDictionary(random, BigInteger, TimeSpan);

        // TestEnum -> *
        Value25 = ImmutableDictionary(random, Enum<TestEnum>, Int32);
        Value26 = ImmutableDictionary(random, Enum<TestEnum>, Int64);
        Value27 = ImmutableDictionary(random, Enum<TestEnum>, BigInteger);
        Value28 = ImmutableDictionary(random, Enum<TestEnum>, Enum<TestEnum>);
        Value29 = ImmutableDictionary(random, Enum<TestEnum>, Boolean);
        Value30 = ImmutableDictionary(random, Enum<TestEnum>, String);
        Value31 = ImmutableDictionary(random, Enum<TestEnum>, DateTimeOffset);
        Value32 = ImmutableDictionary(random, Enum<TestEnum>, TimeSpan);

        // bool -> *
        Value33 = ImmutableDictionary(random, Boolean, Int32);
        Value34 = ImmutableDictionary(random, Boolean, Int64);
        Value35 = ImmutableDictionary(random, Boolean, BigInteger);
        Value36 = ImmutableDictionary(random, Boolean, Enum<TestEnum>);
        Value37 = ImmutableDictionary(random, Boolean, Boolean);
        Value38 = ImmutableDictionary(random, Boolean, String);
        Value39 = ImmutableDictionary(random, Boolean, DateTimeOffset);
        Value40 = ImmutableDictionary(random, Boolean, TimeSpan);

        // string -> *
        Value41 = ImmutableDictionary(random, String, Int32);
        Value42 = ImmutableDictionary(random, String, Int64);
        Value43 = ImmutableDictionary(random, String, BigInteger);
        Value44 = ImmutableDictionary(random, String, Enum<TestEnum>);
        Value45 = ImmutableDictionary(random, String, Boolean);
        Value46 = ImmutableDictionary(random, String, String);
        Value47 = ImmutableDictionary(random, String, DateTimeOffset);
        Value48 = ImmutableDictionary(random, String, TimeSpan);

        // DateTimeOffset -> *
        Value49 = ImmutableDictionary(random, DateTimeOffset, Int32);
        Value50 = ImmutableDictionary(random, DateTimeOffset, Int64);
        Value51 = ImmutableDictionary(random, DateTimeOffset, BigInteger);
        Value52 = ImmutableDictionary(random, DateTimeOffset, Enum<TestEnum>);
        Value53 = ImmutableDictionary(random, DateTimeOffset, Boolean);
        Value54 = ImmutableDictionary(random, DateTimeOffset, String);
        Value55 = ImmutableDictionary(random, DateTimeOffset, DateTimeOffset);
        Value56 = ImmutableDictionary(random, DateTimeOffset, TimeSpan);

        // TimeSpan -> *
        Value57 = ImmutableDictionary(random, TimeSpan, Int32);
        Value58 = ImmutableDictionary(random, TimeSpan, Int64);
        Value59 = ImmutableDictionary(random, TimeSpan, BigInteger);
        Value60 = ImmutableDictionary(random, TimeSpan, Enum<TestEnum>);
        Value61 = ImmutableDictionary(random, TimeSpan, Boolean);
        Value62 = ImmutableDictionary(random, TimeSpan, String);
        Value63 = ImmutableDictionary(random, TimeSpan, DateTimeOffset);
        Value64 = ImmutableDictionary(random, TimeSpan, TimeSpan);
    }

    [Property(0)]
    public ImmutableDictionary<int, int> Value1 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<int, int>.Empty;

    [Property(1)]
    public ImmutableDictionary<int, long> Value2 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<int, long>.Empty;

    [Property(2)]
    public ImmutableDictionary<int, System.Numerics.BigInteger> Value3 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<int, System.Numerics.BigInteger>.Empty;

    [Property(3)]
    public ImmutableDictionary<int, TestEnum> Value4 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<int, TestEnum>.Empty;

    [Property(4)]
    public ImmutableDictionary<int, bool> Value5 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<int, bool>.Empty;

    [Property(5)]
    public ImmutableDictionary<int, string> Value6 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<int, string>.Empty;

    [Property(6)]
    public ImmutableDictionary<int, System.DateTimeOffset> Value7 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<int, System.DateTimeOffset>.Empty;

    [Property(7)]
    public ImmutableDictionary<int, System.TimeSpan> Value8 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<int, System.TimeSpan>.Empty;

    [Property(8)]
    public ImmutableDictionary<long, int> Value9 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<long, int>.Empty;

    [Property(9)]
    public ImmutableDictionary<long, long> Value10 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<long, long>.Empty;

    [Property(10)]
    public ImmutableDictionary<long, System.Numerics.BigInteger> Value11 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<long, System.Numerics.BigInteger>.Empty;

    [Property(11)]
    public ImmutableDictionary<long, TestEnum> Value12 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<long, TestEnum>.Empty;

    [Property(12)]
    public ImmutableDictionary<long, bool> Value13 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<long, bool>.Empty;

    [Property(13)]
    public ImmutableDictionary<long, string> Value14 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<long, string>.Empty;

    [Property(14)]
    public ImmutableDictionary<long, System.DateTimeOffset> Value15 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<long, System.DateTimeOffset>.Empty;

    [Property(15)]
    public ImmutableDictionary<long, System.TimeSpan> Value16 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<long, System.TimeSpan>.Empty;

    [Property(16)]
    public ImmutableDictionary<System.Numerics.BigInteger, int> Value17 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<System.Numerics.BigInteger, int>.Empty;

    [Property(17)]
    public ImmutableDictionary<System.Numerics.BigInteger, long> Value18 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<System.Numerics.BigInteger, long>.Empty;

    [Property(18)]
    public ImmutableDictionary<System.Numerics.BigInteger, System.Numerics.BigInteger> Value19 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<System.Numerics.BigInteger, System.Numerics.BigInteger>.Empty;

    [Property(19)]
    public ImmutableDictionary<System.Numerics.BigInteger, TestEnum> Value20 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<System.Numerics.BigInteger, TestEnum>.Empty;

    [Property(20)]
    public ImmutableDictionary<System.Numerics.BigInteger, bool> Value21 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<System.Numerics.BigInteger, bool>.Empty;

    [Property(21)]
    public ImmutableDictionary<System.Numerics.BigInteger, string> Value22 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<System.Numerics.BigInteger, string>.Empty;

    [Property(22)]
    public ImmutableDictionary<System.Numerics.BigInteger, System.DateTimeOffset> Value23 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<System.Numerics.BigInteger, System.DateTimeOffset>.Empty;

    [Property(23)]
    public ImmutableDictionary<System.Numerics.BigInteger, System.TimeSpan> Value24 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<System.Numerics.BigInteger, System.TimeSpan>.Empty;

    [Property(24)]
    public ImmutableDictionary<TestEnum, int> Value25 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<TestEnum, int>.Empty;

    [Property(25)]
    public ImmutableDictionary<TestEnum, long> Value26 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<TestEnum, long>.Empty;

    [Property(26)]
    public ImmutableDictionary<TestEnum, System.Numerics.BigInteger> Value27 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<TestEnum, System.Numerics.BigInteger>.Empty;

    [Property(27)]
    public ImmutableDictionary<TestEnum, TestEnum> Value28 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<TestEnum, TestEnum>.Empty;

    [Property(28)]
    public ImmutableDictionary<TestEnum, bool> Value29 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<TestEnum, bool>.Empty;

    [Property(29)]
    public ImmutableDictionary<TestEnum, string> Value30 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<TestEnum, string>.Empty;

    [Property(30)]
    public ImmutableDictionary<TestEnum, System.DateTimeOffset> Value31 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<TestEnum, System.DateTimeOffset>.Empty;

    [Property(31)]
    public ImmutableDictionary<TestEnum, System.TimeSpan> Value32 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<TestEnum, System.TimeSpan>.Empty;

    [Property(32)]
    public ImmutableDictionary<bool, int> Value33 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<bool, int>.Empty;

    [Property(33)]
    public ImmutableDictionary<bool, long> Value34 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<bool, long>.Empty;

    [Property(34)]
    public ImmutableDictionary<bool, System.Numerics.BigInteger> Value35 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<bool, System.Numerics.BigInteger>.Empty;

    [Property(35)]
    public ImmutableDictionary<bool, TestEnum> Value36 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<bool, TestEnum>.Empty;

    [Property(36)]
    public ImmutableDictionary<bool, bool> Value37 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<bool, bool>.Empty;

    [Property(37)]
    public ImmutableDictionary<bool, string> Value38 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<bool, string>.Empty;

    [Property(38)]
    public ImmutableDictionary<bool, System.DateTimeOffset> Value39 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<bool, System.DateTimeOffset>.Empty;

    [Property(39)]
    public ImmutableDictionary<bool, System.TimeSpan> Value40 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<bool, System.TimeSpan>.Empty;

    [Property(40)]
    public ImmutableDictionary<string, int> Value41 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<string, int>.Empty;

    [Property(41)]
    public ImmutableDictionary<string, long> Value42 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<string, long>.Empty;

    [Property(42)]
    public ImmutableDictionary<string, System.Numerics.BigInteger> Value43 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<string, System.Numerics.BigInteger>.Empty;

    [Property(43)]
    public ImmutableDictionary<string, TestEnum> Value44 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<string, TestEnum>.Empty;

    [Property(44)]
    public ImmutableDictionary<string, bool> Value45 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<string, bool>.Empty;

    [Property(45)]
    public ImmutableDictionary<string, string> Value46 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<string, string>.Empty;

    [Property(46)]
    public ImmutableDictionary<string, System.DateTimeOffset> Value47 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<string, System.DateTimeOffset>.Empty;

    [Property(47)]
    public ImmutableDictionary<string, System.TimeSpan> Value48 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<string, System.TimeSpan>.Empty;

    [Property(48)]
    public ImmutableDictionary<System.DateTimeOffset, int> Value49 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<System.DateTimeOffset, int>.Empty;

    [Property(49)]
    public ImmutableDictionary<System.DateTimeOffset, long> Value50 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<System.DateTimeOffset, long>.Empty;

    [Property(50)]
    public ImmutableDictionary<System.DateTimeOffset, System.Numerics.BigInteger> Value51 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<System.DateTimeOffset, System.Numerics.BigInteger>.Empty;

    [Property(51)]
    public ImmutableDictionary<System.DateTimeOffset, TestEnum> Value52 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<System.DateTimeOffset, TestEnum>.Empty;

    [Property(52)]
    public ImmutableDictionary<System.DateTimeOffset, bool> Value53 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<System.DateTimeOffset, bool>.Empty;

    [Property(53)]
    public ImmutableDictionary<System.DateTimeOffset, string> Value54 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<System.DateTimeOffset, string>.Empty;

    [Property(54)]
    public ImmutableDictionary<System.DateTimeOffset, System.DateTimeOffset> Value55 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<System.DateTimeOffset, System.DateTimeOffset>.Empty;

    [Property(55)]
    public ImmutableDictionary<System.DateTimeOffset, System.TimeSpan> Value56 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<System.DateTimeOffset, System.TimeSpan>.Empty;

    [Property(56)]
    public ImmutableDictionary<System.TimeSpan, int> Value57 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<System.TimeSpan, int>.Empty;

    [Property(57)]
    public ImmutableDictionary<System.TimeSpan, long> Value58 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<System.TimeSpan, long>.Empty;

    [Property(58)]
    public ImmutableDictionary<System.TimeSpan, System.Numerics.BigInteger> Value59 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<System.TimeSpan, System.Numerics.BigInteger>.Empty;

    [Property(59)]
    public ImmutableDictionary<System.TimeSpan, TestEnum> Value60 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<System.TimeSpan, TestEnum>.Empty;

    [Property(60)]
    public ImmutableDictionary<System.TimeSpan, bool> Value61 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<System.TimeSpan, bool>.Empty;

    [Property(61)]
    public ImmutableDictionary<System.TimeSpan, string> Value62 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<System.TimeSpan, string>.Empty;

    [Property(62)]
    public ImmutableDictionary<System.TimeSpan, System.DateTimeOffset> Value63 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<System.TimeSpan, System.DateTimeOffset>.Empty;

    [Property(63)]
    public ImmutableDictionary<System.TimeSpan, System.TimeSpan> Value64 { get; init; }
        = System.Collections.Immutable.ImmutableDictionary<System.TimeSpan, System.TimeSpan>.Empty;

    public bool Equals(RecordClassWithImmutableDictionary? other) => ModelResolver.Equals(this, other);

    public override int GetHashCode() => ModelResolver.GetHashCode(this);
}
