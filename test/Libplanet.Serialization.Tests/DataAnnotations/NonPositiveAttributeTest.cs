using System.Reflection;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.TestUtilities;

namespace Libplanet.Serialization.Tests.DataAnnotations;

public sealed class NonPositiveAttributeTest
{
    [Fact]
    public void AttributeTest()
    {
        var attr = typeof(NonPositiveAttribute).GetCustomAttribute<AttributeUsageAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(AttributeTargets.Property, attr.ValidOn);
        Assert.False(attr.AllowMultiple);
    }

    [Fact]
    public void Compare()
    {
        var obj1 = new TestClass
        {
            Value1 = -1,
            Value2 = -1,
            Value3 = -1,
            Value4 = -1L,
            Value5 = -1f,
            Value6 = -1d,
            Value7 = -1m,
            Value8 = (BigInteger)(-1),
        };
        ModelAssert.DoseNotThrow(obj1);

        var obj2 = new TestClass
        {
            Value1 = 0,
            Value2 = 0,
            Value3 = 0,
            Value4 = 0L,
            Value5 = 0f,
            Value6 = 0d,
            Value7 = 0m,
            Value8 = (BigInteger)0,
        };
        ModelAssert.DoseNotThrow(obj2);
    }

    [Fact]
    public void Compare_To_Constant_Throw()
    {
        var propertyNames = new[]
        {
            nameof(TestClass.Value1),
            nameof(TestClass.Value2),
            nameof(TestClass.Value3),
            nameof(TestClass.Value4),
            nameof(TestClass.Value5),
            nameof(TestClass.Value6),
            nameof(TestClass.Value7),
            nameof(TestClass.Value8),
        };

        var obj1 = new TestClass
        {
            Value1 = 1,
            Value2 = 1,
            Value3 = 1,
            Value4 = 1L,
            Value5 = 1f,
            Value6 = 1d,
            Value7 = 1m,
            Value8 = (BigInteger)1,
        };
        ModelAssert.ThrowsMany(obj1, propertyNames);
    }

    [Fact]
    public void UnsupportedType_Throw()
    {
        var obj1 = new InvalidTestClass();
        ModelAssert.Throws(obj1, nameof(InvalidTestClass.Value1));
    }

    private sealed record class TestClass
    {
        [NonPositive]
        public sbyte Value1 { get; init; }

        [NonPositive]
        public short Value2 { get; init; }

        [NonPositive]
        public int Value3 { get; init; }

        [NonPositive]
        public long Value4 { get; init; }

        [NonPositive]
        public float Value5 { get; init; }

        [NonPositive]
        public double Value6 { get; init; }

        [NonPositive]
        public decimal Value7 { get; init; }

        [NonPositive]
        public BigInteger Value8 { get; init; }
    }

    private sealed record class InvalidTestClass
    {
        [NonPositive]
        public uint Value1 { get; init; }
    }
}
