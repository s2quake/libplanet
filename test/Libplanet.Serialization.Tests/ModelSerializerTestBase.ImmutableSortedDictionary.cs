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
    public void ImmutableSortedDictionaryProperty_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedObject = new RecordClassWithImmutableSortedDictionary(random);
        var serialized = Serialize(expectedObject);
        var actualObject = Deserialize<RecordClassWithImmutableSortedDictionary>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }

    [Theory]
    [InlineData(0)]
    [ClassData(typeof(RandomSeedsData))]
    public void ImmutableSortedDictionary_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedObject = new RecordClassWithImmutableSortedDictionary(random);
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

[Model("Libplanet_Serialization_Tests_ModelSerializerTest_RecordClassWithImmutableSortedDictionary", Version = 1)]
public sealed record class RecordClassWithImmutableSortedDictionary
    : IEquatable<RecordClassWithImmutableSortedDictionary>
{
    public RecordClassWithImmutableSortedDictionary()
    {
    }

    public RecordClassWithImmutableSortedDictionary(Random random)
    {
        // int -> *
        Value1 = ImmutableSortedDictionary(random, Int32, Int32);
        Value2 = ImmutableSortedDictionary(random, Int32, Int64);
        Value3 = ImmutableSortedDictionary(random, Int32, BigInteger);
        Value4 = ImmutableSortedDictionary(random, Int32, Enum<TestEnum>);
        Value5 = ImmutableSortedDictionary(random, Int32, Boolean);
        Value6 = ImmutableSortedDictionary(random, Int32, String);
        Value7 = ImmutableSortedDictionary(random, Int32, DateTimeOffset);
        Value8 = ImmutableSortedDictionary(random, Int32, TimeSpan);

        // long -> *
        Value9 = ImmutableSortedDictionary(random, Int64, Int32);
        Value10 = ImmutableSortedDictionary(random, Int64, Int64);
        Value11 = ImmutableSortedDictionary(random, Int64, BigInteger);
        Value12 = ImmutableSortedDictionary(random, Int64, Enum<TestEnum>);
        Value13 = ImmutableSortedDictionary(random, Int64, Boolean);
        Value14 = ImmutableSortedDictionary(random, Int64, String);
        Value15 = ImmutableSortedDictionary(random, Int64, DateTimeOffset);
        Value16 = ImmutableSortedDictionary(random, Int64, TimeSpan);

        // BigInteger -> *
        Value17 = ImmutableSortedDictionary(random, BigInteger, Int32);
        Value18 = ImmutableSortedDictionary(random, BigInteger, Int64);
        Value19 = ImmutableSortedDictionary(random, BigInteger, BigInteger);
        Value20 = ImmutableSortedDictionary(random, BigInteger, Enum<TestEnum>);
        Value21 = ImmutableSortedDictionary(random, BigInteger, Boolean);
        Value22 = ImmutableSortedDictionary(random, BigInteger, String);
        Value23 = ImmutableSortedDictionary(random, BigInteger, DateTimeOffset);
        Value24 = ImmutableSortedDictionary(random, BigInteger, TimeSpan);

        // TestEnum -> *
        Value25 = ImmutableSortedDictionary(random, Enum<TestEnum>, Int32);
        Value26 = ImmutableSortedDictionary(random, Enum<TestEnum>, Int64);
        Value27 = ImmutableSortedDictionary(random, Enum<TestEnum>, BigInteger);
        Value28 = ImmutableSortedDictionary(random, Enum<TestEnum>, Enum<TestEnum>);
        Value29 = ImmutableSortedDictionary(random, Enum<TestEnum>, Boolean);
        Value30 = ImmutableSortedDictionary(random, Enum<TestEnum>, String);
        Value31 = ImmutableSortedDictionary(random, Enum<TestEnum>, DateTimeOffset);
        Value32 = ImmutableSortedDictionary(random, Enum<TestEnum>, TimeSpan);

        // bool -> *
        Value33 = ImmutableSortedDictionary(random, Boolean, Int32);
        Value34 = ImmutableSortedDictionary(random, Boolean, Int64);
        Value35 = ImmutableSortedDictionary(random, Boolean, BigInteger);
        Value36 = ImmutableSortedDictionary(random, Boolean, Enum<TestEnum>);
        Value37 = ImmutableSortedDictionary(random, Boolean, Boolean);
        Value38 = ImmutableSortedDictionary(random, Boolean, String);
        Value39 = ImmutableSortedDictionary(random, Boolean, DateTimeOffset);
        Value40 = ImmutableSortedDictionary(random, Boolean, TimeSpan);

        // string -> *
        Value41 = ImmutableSortedDictionary(random, String, Int32);
        Value42 = ImmutableSortedDictionary(random, String, Int64);
        Value43 = ImmutableSortedDictionary(random, String, BigInteger);
        Value44 = ImmutableSortedDictionary(random, String, Enum<TestEnum>);
        Value45 = ImmutableSortedDictionary(random, String, Boolean);
        Value46 = ImmutableSortedDictionary(random, String, String);
        Value47 = ImmutableSortedDictionary(random, String, DateTimeOffset);
        Value48 = ImmutableSortedDictionary(random, String, TimeSpan);

        // DateTimeOffset -> *
        Value49 = ImmutableSortedDictionary(random, DateTimeOffset, Int32);
        Value50 = ImmutableSortedDictionary(random, DateTimeOffset, Int64);
        Value51 = ImmutableSortedDictionary(random, DateTimeOffset, BigInteger);
        Value52 = ImmutableSortedDictionary(random, DateTimeOffset, Enum<TestEnum>);
        Value53 = ImmutableSortedDictionary(random, DateTimeOffset, Boolean);
        Value54 = ImmutableSortedDictionary(random, DateTimeOffset, String);
        Value55 = ImmutableSortedDictionary(random, DateTimeOffset, DateTimeOffset);
        Value56 = ImmutableSortedDictionary(random, DateTimeOffset, TimeSpan);

        // TimeSpan -> *
        Value57 = ImmutableSortedDictionary(random, TimeSpan, Int32);
        Value58 = ImmutableSortedDictionary(random, TimeSpan, Int64);
        Value59 = ImmutableSortedDictionary(random, TimeSpan, BigInteger);
        Value60 = ImmutableSortedDictionary(random, TimeSpan, Enum<TestEnum>);
        Value61 = ImmutableSortedDictionary(random, TimeSpan, Boolean);
        Value62 = ImmutableSortedDictionary(random, TimeSpan, String);
        Value63 = ImmutableSortedDictionary(random, TimeSpan, DateTimeOffset);
        Value64 = ImmutableSortedDictionary(random, TimeSpan, TimeSpan);
    }

    [Property(0)]
    public ImmutableSortedDictionary<int, int> Value1 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<int, int>.Empty;

    [Property(1)]
    public ImmutableSortedDictionary<int, long> Value2 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<int, long>.Empty;

    [Property(2)]
    public ImmutableSortedDictionary<int, System.Numerics.BigInteger> Value3 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<int, System.Numerics.BigInteger>.Empty;

    [Property(3)]
    public ImmutableSortedDictionary<int, TestEnum> Value4 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<int, TestEnum>.Empty;

    [Property(4)]
    public ImmutableSortedDictionary<int, bool> Value5 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<int, bool>.Empty;

    [Property(5)]
    public ImmutableSortedDictionary<int, string> Value6 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<int, string>.Empty;

    [Property(6)]
    public ImmutableSortedDictionary<int, System.DateTimeOffset> Value7 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<int, System.DateTimeOffset>.Empty;

    [Property(7)]
    public ImmutableSortedDictionary<int, System.TimeSpan> Value8 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<int, System.TimeSpan>.Empty;

    [Property(8)]
    public ImmutableSortedDictionary<long, int> Value9 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<long, int>.Empty;

    [Property(9)]
    public ImmutableSortedDictionary<long, long> Value10 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<long, long>.Empty;

    [Property(10)]
    public ImmutableSortedDictionary<long, System.Numerics.BigInteger> Value11 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<long, System.Numerics.BigInteger>.Empty;

    [Property(11)]
    public ImmutableSortedDictionary<long, TestEnum> Value12 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<long, TestEnum>.Empty;

    [Property(12)]
    public ImmutableSortedDictionary<long, bool> Value13 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<long, bool>.Empty;

    [Property(13)]
    public ImmutableSortedDictionary<long, string> Value14 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<long, string>.Empty;

    [Property(14)]
    public ImmutableSortedDictionary<long, System.DateTimeOffset> Value15 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<long, System.DateTimeOffset>.Empty;

    [Property(15)]
    public ImmutableSortedDictionary<long, System.TimeSpan> Value16 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<long, System.TimeSpan>.Empty;

    [Property(16)]
    public ImmutableSortedDictionary<System.Numerics.BigInteger, int> Value17 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<System.Numerics.BigInteger, int>.Empty;

    [Property(17)]
    public ImmutableSortedDictionary<System.Numerics.BigInteger, long> Value18 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<System.Numerics.BigInteger, long>.Empty;

    [Property(18)]
    public ImmutableSortedDictionary<System.Numerics.BigInteger, System.Numerics.BigInteger> Value19 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<System.Numerics.BigInteger, System.Numerics.BigInteger>.Empty;

    [Property(19)]
    public ImmutableSortedDictionary<System.Numerics.BigInteger, TestEnum> Value20 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<System.Numerics.BigInteger, TestEnum>.Empty;

    [Property(20)]
    public ImmutableSortedDictionary<System.Numerics.BigInteger, bool> Value21 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<System.Numerics.BigInteger, bool>.Empty;

    [Property(21)]
    public ImmutableSortedDictionary<System.Numerics.BigInteger, string> Value22 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<System.Numerics.BigInteger, string>.Empty;

    [Property(22)]
    public ImmutableSortedDictionary<System.Numerics.BigInteger, System.DateTimeOffset> Value23 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<System.Numerics.BigInteger, System.DateTimeOffset>.Empty;

    [Property(23)]
    public ImmutableSortedDictionary<System.Numerics.BigInteger, System.TimeSpan> Value24 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<System.Numerics.BigInteger, System.TimeSpan>.Empty;

    [Property(24)]
    public ImmutableSortedDictionary<TestEnum, int> Value25 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<TestEnum, int>.Empty;

    [Property(25)]
    public ImmutableSortedDictionary<TestEnum, long> Value26 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<TestEnum, long>.Empty;

    [Property(26)]
    public ImmutableSortedDictionary<TestEnum, System.Numerics.BigInteger> Value27 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<TestEnum, System.Numerics.BigInteger>.Empty;

    [Property(27)]
    public ImmutableSortedDictionary<TestEnum, TestEnum> Value28 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<TestEnum, TestEnum>.Empty;

    [Property(28)]
    public ImmutableSortedDictionary<TestEnum, bool> Value29 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<TestEnum, bool>.Empty;

    [Property(29)]
    public ImmutableSortedDictionary<TestEnum, string> Value30 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<TestEnum, string>.Empty;

    [Property(30)]
    public ImmutableSortedDictionary<TestEnum, System.DateTimeOffset> Value31 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<TestEnum, System.DateTimeOffset>.Empty;

    [Property(31)]
    public ImmutableSortedDictionary<TestEnum, System.TimeSpan> Value32 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<TestEnum, System.TimeSpan>.Empty;

    [Property(32)]
    public ImmutableSortedDictionary<bool, int> Value33 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<bool, int>.Empty;

    [Property(33)]
    public ImmutableSortedDictionary<bool, long> Value34 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<bool, long>.Empty;

    [Property(34)]
    public ImmutableSortedDictionary<bool, System.Numerics.BigInteger> Value35 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<bool, System.Numerics.BigInteger>.Empty;

    [Property(35)]
    public ImmutableSortedDictionary<bool, TestEnum> Value36 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<bool, TestEnum>.Empty;

    [Property(36)]
    public ImmutableSortedDictionary<bool, bool> Value37 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<bool, bool>.Empty;

    [Property(37)]
    public ImmutableSortedDictionary<bool, string> Value38 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<bool, string>.Empty;

    [Property(38)]
    public ImmutableSortedDictionary<bool, System.DateTimeOffset> Value39 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<bool, System.DateTimeOffset>.Empty;

    [Property(39)]
    public ImmutableSortedDictionary<bool, System.TimeSpan> Value40 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<bool, System.TimeSpan>.Empty;

    [Property(40)]
    public ImmutableSortedDictionary<string, int> Value41 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<string, int>.Empty;

    [Property(41)]
    public ImmutableSortedDictionary<string, long> Value42 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<string, long>.Empty;

    [Property(42)]
    public ImmutableSortedDictionary<string, System.Numerics.BigInteger> Value43 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<string, System.Numerics.BigInteger>.Empty;

    [Property(43)]
    public ImmutableSortedDictionary<string, TestEnum> Value44 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<string, TestEnum>.Empty;

    [Property(44)]
    public ImmutableSortedDictionary<string, bool> Value45 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<string, bool>.Empty;

    [Property(45)]
    public ImmutableSortedDictionary<string, string> Value46 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<string, string>.Empty;

    [Property(46)]
    public ImmutableSortedDictionary<string, System.DateTimeOffset> Value47 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<string, System.DateTimeOffset>.Empty;

    [Property(47)]
    public ImmutableSortedDictionary<string, System.TimeSpan> Value48 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<string, System.TimeSpan>.Empty;

    [Property(48)]
    public ImmutableSortedDictionary<System.DateTimeOffset, int> Value49 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<System.DateTimeOffset, int>.Empty;

    [Property(49)]
    public ImmutableSortedDictionary<System.DateTimeOffset, long> Value50 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<System.DateTimeOffset, long>.Empty;

    [Property(50)]
    public ImmutableSortedDictionary<System.DateTimeOffset, System.Numerics.BigInteger> Value51 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<System.DateTimeOffset, System.Numerics.BigInteger>.Empty;

    [Property(51)]
    public ImmutableSortedDictionary<System.DateTimeOffset, TestEnum> Value52 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<System.DateTimeOffset, TestEnum>.Empty;

    [Property(52)]
    public ImmutableSortedDictionary<System.DateTimeOffset, bool> Value53 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<System.DateTimeOffset, bool>.Empty;

    [Property(53)]
    public ImmutableSortedDictionary<System.DateTimeOffset, string> Value54 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<System.DateTimeOffset, string>.Empty;

    [Property(54)]
    public ImmutableSortedDictionary<System.DateTimeOffset, System.DateTimeOffset> Value55 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<System.DateTimeOffset, System.DateTimeOffset>.Empty;

    [Property(55)]
    public ImmutableSortedDictionary<System.DateTimeOffset, System.TimeSpan> Value56 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<System.DateTimeOffset, System.TimeSpan>.Empty;

    [Property(56)]
    public ImmutableSortedDictionary<System.TimeSpan, int> Value57 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<System.TimeSpan, int>.Empty;

    [Property(57)]
    public ImmutableSortedDictionary<System.TimeSpan, long> Value58 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<System.TimeSpan, long>.Empty;

    [Property(58)]
    public ImmutableSortedDictionary<System.TimeSpan, System.Numerics.BigInteger> Value59 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<System.TimeSpan, System.Numerics.BigInteger>.Empty;

    [Property(59)]
    public ImmutableSortedDictionary<System.TimeSpan, TestEnum> Value60 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<System.TimeSpan, TestEnum>.Empty;

    [Property(60)]
    public ImmutableSortedDictionary<System.TimeSpan, bool> Value61 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<System.TimeSpan, bool>.Empty;

    [Property(61)]
    public ImmutableSortedDictionary<System.TimeSpan, string> Value62 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<System.TimeSpan, string>.Empty;

    [Property(62)]
    public ImmutableSortedDictionary<System.TimeSpan, System.DateTimeOffset> Value63 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<System.TimeSpan, System.DateTimeOffset>.Empty;

    [Property(63)]
    public ImmutableSortedDictionary<System.TimeSpan, System.TimeSpan> Value64 { get; init; }
        = System.Collections.Immutable.ImmutableSortedDictionary<System.TimeSpan, System.TimeSpan>.Empty;

    public bool Equals(RecordClassWithImmutableSortedDictionary? other) => ModelResolver.Equals(this, other);

    public override int GetHashCode() => ModelResolver.GetHashCode(this);
}
