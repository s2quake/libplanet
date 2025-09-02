using System.Reflection;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.TestUtilities;
using Libplanet.Types;

namespace Libplanet.Serialization.Tests.DataAnnotations;

public sealed class LessThanAttributeTest
{
    [Fact]
    public void AttributeTest()
    {
        var attr = typeof(LessThanAttribute).GetCustomAttribute<AttributeUsageAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(AttributeTargets.Property, attr.ValidOn);
        Assert.False(attr.AllowMultiple);
    }

    public static TheoryDataRow<object, object>[] ValidValues =>
    [
        new(false, true),
        new(0, 1),
        new(0L, 1L),
        new(0f, 1f),
        new(0d, 1d),
        new(0m, 1m),
        new((BigInteger)0, (BigInteger)1),
        new('a', 'z'),
        new("a", "z"),
    ];

    [Theory]
    [MemberData(nameof(ValidValues))]
    public void Compare(object value1, object value2)
    {
        var obj1 = new TestClass1
        {
            Value1 = value1,
            Value2 = value2,
        };
        ModelAssert.DoseNotThrow(obj1);
    }

    [Fact]
    public void Compare_Address()
    {
        var obj1 = new TestClass1
        {
            Value1 = Address.Parse("0x1c54b2F83D26E2db2D93dE4539c301d8aE32E69d"),
            Value2 = Address.Parse("0x27A6F7321C93DE392d1078A7A3BdC62E03962cF7"),
        };
        ModelAssert.DoseNotThrow(obj1);
    }

    public static TheoryDataRow<object, object>[] InvalidValues =>
    [
        new(true, false),
        new(1, 0),  
        new(1L, 0L),
        new(1f, 0f),
        new(1d, 0d),
        new(1m, 0m),
        new((BigInteger)1, (BigInteger)0),
        new('z', 'a'),
        new("z", "a"),
    ];

    [Theory]
    [MemberData(nameof(InvalidValues))]
    public void Compare_Throw(object value1, object value2)
    {
        var obj1 = new TestClass1
        {
            Value1 = value1,
            Value2 = value2,
        };
        ModelAssert.Throws(obj1);

        var obj2 = new TestClass1
        {
            Value1 = value1,
            Value2 = value1,
        };
        ModelAssert.Throws(obj2);
    }

    [Fact]
    public void Compare_Address_Throw()
    {
        var obj1 = new TestClass1
        {
            Value1 = Address.Parse("0x27A6F7321C93DE392d1078A7A3BdC62E03962cF7"),
            Value2 = Address.Parse("0x1c54b2F83D26E2db2D93dE4539c301d8aE32E69d"),
        };
        ModelAssert.Throws(obj1);

        var obj2 = new TestClass1
        {
            Value1 = Address.Parse("0x1c54b2F83D26E2db2D93dE4539c301d8aE32E69d"),
            Value2 = Address.Parse("0x1c54b2F83D26E2db2D93dE4539c301d8aE32E69d"),
        };
        ModelAssert.Throws(obj2);
    }

    [Fact]
    public void Compare_To_Constant()
    {
        var obj1 = new TestClass2
        {
            Value1 = false,
            Value2 = -1,
            Value3 = -1L,
            Value4 = -1f,
            Value5 = -1d,
            Value6 = -1m,
            Value7 = -1,
            Value8 = 'a',
            Value9 = "A"
        };
        ModelAssert.DoseNotThrow(obj1);
    }

    [Fact]
    public void Compare_To_Constant_Throw()
    {
        var propertyNames = new[]
        {
            nameof(TestClass2.Value1),
            nameof(TestClass2.Value2),
            nameof(TestClass2.Value3),
            nameof(TestClass2.Value4),
            nameof(TestClass2.Value5),
            nameof(TestClass2.Value6),
            nameof(TestClass2.Value7),
            nameof(TestClass2.Value8),
            nameof(TestClass2.Value9),
        };

        var obj1 = new TestClass2
        {
            Value1 = true,
            Value2 = 1,
            Value3 = 1L,
            Value4 = 1f,
            Value5 = 1d,
            Value6 = 1m,
            Value7 = 1,
            Value8 = 'c',
            Value9 = "C"
        };
        ModelAssert.ThrowsMany(obj1, propertyNames);

        var obj2 = new TestClass2
        {
            Value1 = true,
            Value2 = 0,
            Value3 = 0L,
            Value4 = 0f,
            Value5 = 0d,
            Value6 = 0m,
            Value7 = 0,
            Value8 = 'b',
            Value9 = "B"
        };
        ModelAssert.ThrowsMany(obj2, propertyNames);
    }

    private sealed record class TestClass1
    {
        [LessThan(targetType: null, nameof(Value2))]
        public required object Value1 { get; init; }

        public required object Value2 { get; init; }
    }

    private sealed record class TestClass2
    {
        [LessThan(true)]
        public bool Value1 { get; init; }

        [LessThan(0)]
        public int Value2 { get; init; }

        [LessThan(0L)]
        public long Value3 { get; init; }

        [LessThan(0f)]
        public float Value4 { get; init; }

        [LessThan(0d)]
        public double Value5 { get; init; }

        [LessThan("0", typeof(decimal))]
        public decimal Value6 { get; init; }

        [LessThan("0", typeof(BigInteger))]
        public BigInteger Value7 { get; init; }

        [LessThan('b')]
        public char Value8 { get; init; }

        [LessThan("B")]
        public string Value9 { get; init; } = string.Empty;
    }
}
