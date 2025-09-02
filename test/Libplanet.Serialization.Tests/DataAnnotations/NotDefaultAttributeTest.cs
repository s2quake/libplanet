using System.Reflection;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.TestUtilities;
using Libplanet.Types;

namespace Libplanet.Serialization.Tests.DataAnnotations;

public sealed class NotDefaultAttributeTest
{
    [Fact]
    public void AttributeTest()
    {
        var attr = typeof(NotDefaultAttribute).GetCustomAttribute<AttributeUsageAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(AttributeTargets.Property, attr.ValidOn);
        Assert.False(attr.AllowMultiple);
    }

    [Fact]
    public void Validate_Test()
    {
        var obj1 = new TestClass
        {
            Value1 = 1,
            Value2 = Address.Parse("0x27A6F7321C93DE392d1078A7A3BdC62E03962cF7"),
            Value3 = [1, 2, 3],
        };
        ModelAssert.DoseNotThrow(obj1);
    }

    [Fact]
    public void Validate_Throw()
    {
        string[] propertyNames =
        [
            nameof(TestClass.Value1),
            nameof(TestClass.Value2),
            nameof(TestClass.Value3)
        ];

        var obj1 = new TestClass();
        ModelAssert.ThrowsMany(obj1, propertyNames);
    }

    [Fact]
    public void UnsupportedType_Throw()
    {
        string[] propertyNames =
        [
            nameof(InvalidTestClass.Value1),
            nameof(InvalidTestClass.Value2)
        ];
        var obj1 = new InvalidTestClass();
        ModelAssert.ThrowsMany(obj1, propertyNames);
    }

    private sealed record class TestClass
    {
        [NotDefault]
        public int Value1 { get; init; }

        [NotDefault]
        public Address Value2 { get; init; }

        [NotDefault]
        public ImmutableArray<int> Value3 { get; init; }
    }

    private sealed record class InvalidTestClass
    {
        [NotDefault]
        public int? Value1 { get; init; }

        [NotDefault]
        public object Value2 { get; init; } = new object();
    }
}
