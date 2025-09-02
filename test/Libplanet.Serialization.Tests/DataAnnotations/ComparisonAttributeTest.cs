#pragma warning disable S3459 // Unassigned members should be removed
using Libplanet.Serialization.DataAnnotations;
using Libplanet.TestUtilities;
using Libplanet.Types;

namespace Libplanet.Serialization.Tests.DataAnnotations;

public sealed class ComparisonAttributeTest
{
    [Fact]
    public void BaseTest()
    {
        ModelAssert.DoseNotThrow(new TestClass());
    }

    [Fact]
    public void PropertyEmpty_Throw()
    {
        ModelAssert.Throws(new TestClass1_A(), nameof(TestClass1_A.Value1));
        ModelAssert.Throws(new TestClass1_B(), nameof(TestClass1_B.Value1));
    }

    [Fact]
    public void NonExistentProperty_Throw()
    {
        ModelAssert.Throws(new TestClass2_A(), nameof(TestClass2_A.Value1));
        ModelAssert.Throws(new TestClass2_B(), nameof(TestClass2_B.Value1));
    }

    [Fact]
    public void SelfProperty_Throw()
    {
        ModelAssert.Throws(new TestClass3_A(), nameof(TestClass3_A.Value1));
    }

    [Fact]
    public void PropertyNotIComparable_Throw()
    {
        ModelAssert.Throws(new TestClass4_A(), nameof(TestClass4_A.Value1));
        ModelAssert.Throws(new TestClass4_B(), nameof(TestClass4_B.Value1));
    }

    [Fact]
    public void CannotParseProperty_Throw()
    {
        ModelAssert.Throws(new TestClass5_A(), nameof(TestClass5_A.Value1));
        ModelAssert.Throws(new TestClass5_B(), nameof(TestClass5_B.Value1));
        ModelAssert.Throws(new TestClass5_C(), nameof(TestClass5_C.Value1));
    }

    [Fact]
    public void NotComparable_Throw()
    {
        ModelAssert.Throws(new TestClass6_A(), nameof(TestClass6_A.Value1));
        ModelAssert.Throws(new TestClass6_B(), nameof(TestClass6_B.Value1));
    }

    [Fact]
    public void NullValue_Throw()
    {
        ModelAssert.Throws(new TestClass7(), nameof(TestClass7.Value1));
    }

    [Fact]
    public void DifferenceType_Throw()
    {
        ModelAssert.Throws(new TestClass8(), nameof(TestClass8.Value1));
    }

    [Fact]
    public void DifferenceValue_Throw()
    {
        ModelAssert.Throws(new TestClass9(), nameof(TestClass9.Value1));
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    private sealed class TestAttribute : ComparisonAttribute
    {
        public TestAttribute(Type? targetType, string propertyName)
        : base(targetType, propertyName)
        {
        }

        public TestAttribute(object value)
            : base(value)
        {
        }

        public TestAttribute(string textValue, Type valueType)
            : base(textValue, valueType)
        {
        }

        protected override bool Compare(IComparable value, IComparable target)
            => value.CompareTo(target) is 0;

        protected override string FormatErrorMessage(
            string memberName, Type declaringType, IComparable value, IComparable target)
            => string.Empty;
    }

    private sealed class TestClass
    {
        [Test(targetType: null, propertyName: nameof(Value2))]
        public int Value1 { get; set; }

        [Test(targetType: typeof(TestStaticClass1), propertyName: nameof(TestStaticClass1.Value1))]
        public int Value2 { get; set; }

        [Test(0)]
        public int Value3 { get; set; }

        [Test("0", typeof(int))]
        public int Value4 { get; set; }

        [Test("0", typeof(BigInteger))]
        public BigInteger Value5 { get; set; }

        [Test("0x27A6F7321C93DE392d1078A7A3BdC62E03962cF7", typeof(Address))]
        public Address Value6 { get; set; } = Address.Parse("0x27A6F7321C93DE392d1078A7A3BdC62E03962cF7");
    }

    private sealed class TestClass1_A
    {
        [Test(targetType: null, propertyName: "")]
        public int Value1 { get; set; }
    }

    private sealed class TestClass1_B
    {
        [Test(targetType: typeof(TestStaticClass1), propertyName: "")]
        public int Value1 { get; set; }
    }

    private sealed class TestClass2_A
    {
        [Test(targetType: null, propertyName: "a")]
        public int Value1 { get; set; }
    }

    private sealed class TestClass2_B
    {
        [Test(targetType: typeof(TestStaticClass1), propertyName: "a")]
        public int Value1 { get; set; }
    }

    private sealed class TestClass3_A
    {
        [Test(targetType: null, propertyName: nameof(Value1))]
        public int Value1 { get; set; }
    }

    private sealed class TestClass4_A
    {
        [Test(targetType: null, propertyName: nameof(Value2))]
        public int Value1 { get; set; }

        public object? Value2 { get; set; }
    }

    private sealed class TestClass4_B
    {
        [Test(targetType: typeof(TestStaticClass1), propertyName: nameof(TestStaticClass1.Value2))]
        public int Value1 { get; set; }
    }

    private sealed class TestClass5_A
    {
        [Test("a", typeof(BigInteger))]
        public BigInteger Value1 { get; set; }
    }

    private sealed class TestClass5_B
    {
        [Test("a", typeof(int))]
        public int Value1 { get; set; }
    }

    private sealed class TestClass5_C
    {
        [Test("a", typeof(object))]
        public int Value1 { get; set; }
    }

    private sealed class TestClass6_A
    {
        [Test(0)]
        public object Value1 { get; set; } = new object();
    }

    private sealed class TestClass6_B
    {
        [Test(0)]
        public int? Value1 { get; set; }
    }

    private sealed class TestClass7
    {
        [Test(null!)]
        public int Value1 { get; set; }
    }

    private sealed class TestClass8
    {
        [Test(0)]
        public long Value1 { get; set; }
    }

    private sealed class TestClass9
    {
        [Test(0)]
        public int Value1 { get; set; } = 1;
    }

    private static class TestStaticClass1
    {
        public static int Value1 { get; set; }

        public static object? Value2 { get; set; }
    }
}