using Libplanet.Types.Assets;
using Xunit;
using static Libplanet.Tests.TestUtils;

namespace Libplanet.Tests.Assets;

public class FungibleAssetValueTest
{
    private static readonly Currency FOO = Currency.Create("FOO", 2);
    private static readonly Currency BAR = Currency.Create("BAR", 0);
    private static readonly Currency BARMAX = Currency.Create("BAR", 0, 100000);

    [Fact]
    public void Constructor()
    {
        FungibleAssetValue v;
        v = FungibleAssetValue.Create(FOO, 123, 45);
        Assert.Equal(FungibleAssetValue.Create(FOO, 123, 45), v);
        Assert.Equal(12345, v.RawValue);
        Assert.Equal(123, v.MajorUnit);
        Assert.Equal(45, v.MinorUnit);
        Assert.Equal(1, v.Sign);

        v = FungibleAssetValue.Create(FOO, 456, 9);
        Assert.Equal(FungibleAssetValue.Create(FOO, 456, 9), v);
        Assert.Equal(45609, v.RawValue);
        Assert.Equal(456, v.MajorUnit);
        Assert.Equal(9, v.MinorUnit);
        Assert.Equal(1, v.Sign);

        v = FungibleAssetValue.Create(FOO, 0, 10);
        Assert.Equal(FungibleAssetValue.Create(FOO, 0, 10), v);
        Assert.Equal(10, v.RawValue);
        Assert.Equal(0, v.MajorUnit);
        Assert.Equal(10, v.MinorUnit);
        Assert.Equal(1, v.Sign);

        v = FungibleAssetValue.Create(FOO, 0, 9);
        Assert.Equal(FungibleAssetValue.Create(FOO, 0, 9), v);
        Assert.Equal(9, v.RawValue);
        Assert.Equal(0, v.MajorUnit);
        Assert.Equal(9, v.MinorUnit);
        Assert.Equal(1, v.Sign);

        v = FungibleAssetValue.Create(FOO, -789, -1);
        Assert.Equal(FungibleAssetValue.Create(FOO, -789, -1), v);
        Assert.Equal(-78901, v.RawValue);
        Assert.Equal(-789, v.MajorUnit);
        Assert.Equal(-1, v.MinorUnit);
        Assert.Equal(-1, v.Sign);

        v = FungibleAssetValue.Create(FOO, 0, -2);
        Assert.Equal(FungibleAssetValue.Create(FOO, 0, -2), v);
        Assert.Equal(-2, v.RawValue);
        Assert.Equal(0, v.MajorUnit);
        Assert.Equal(-2, v.MinorUnit);
        Assert.Equal(-1, v.Sign);

        v = FungibleAssetValue.Create(FOO, 123, 0);
        Assert.Equal(FungibleAssetValue.Create(FOO, 123, 0), v);
        Assert.Equal(12300, v.RawValue);
        Assert.Equal(123, v.MajorUnit);
        Assert.Equal(0, v.MinorUnit);
        Assert.Equal(1, v.Sign);

        v = FungibleAssetValue.Create(BAR, 1, 0);
        Assert.Equal(FungibleAssetValue.Create(BAR, 1, 0), v);
        Assert.Equal(FungibleAssetValue.Create(BAR, 1), v);
        Assert.Equal(1, v.RawValue);
        Assert.Equal(1, v.MajorUnit);
        Assert.Equal(0, v.MinorUnit);
        Assert.Equal(1, v.Sign);

        v = FungibleAssetValue.Create(FOO, 0, 0);
        Assert.Equal(FungibleAssetValue.Create(FOO, 0, 0), v);
        Assert.Equal(FungibleAssetValue.Create(FOO), v);
        Assert.Equal(0, v.RawValue);
        Assert.Equal(0, v.MajorUnit);
        Assert.Equal(0, v.MinorUnit);
        Assert.Equal(0, v.Sign);

        Assert.Throws<ArgumentOutOfRangeException>(() => FungibleAssetValue.Create(FOO, 1, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => FungibleAssetValue.Create(BAR, 1, -100));
    }

    [Fact]
    public void Equality()
    {
        var foo100a = FungibleAssetValue.Create(FOO, 100);
        var foo100b = FungibleAssetValue.Create(FOO, 100);
        var foo200a = FungibleAssetValue.Create(FOO, 200);
        var foo200b = FungibleAssetValue.Create(FOO, 200);
        var bar100a = FungibleAssetValue.Create(BAR, 100);
        var bar100b = FungibleAssetValue.Create(BAR, 100);
        var bar200a = FungibleAssetValue.Create(BAR, 200);
        var bar200b = FungibleAssetValue.Create(BAR, 200);
        var barmax100 = FungibleAssetValue.Create(BARMAX, 100);

        Assert.Equal(foo100b, foo100a);
        Assert.Equal(foo100b.GetHashCode(), foo100a.GetHashCode());
        Assert.True(foo100b.Equals((object)foo100a));
        Assert.True(foo100b == foo100a);
        Assert.False(foo100b != foo100a);
        Assert.Equal(foo200b, foo200a);
        Assert.Equal(foo200b.GetHashCode(), foo200a.GetHashCode());
        Assert.True(foo200b.Equals((object)foo200a));
        Assert.True(foo200b == foo200a);
        Assert.False(foo200b != foo200a);
        Assert.Equal(bar100b, bar100a);
        Assert.Equal(bar100b.GetHashCode(), bar100a.GetHashCode());
        Assert.True(bar100b.Equals((object)bar100a));
        Assert.True(bar100b == bar100a);
        Assert.False(bar100b != bar100a);
        Assert.Equal(bar200b, bar200a);
        Assert.Equal(bar200b.GetHashCode(), bar200a.GetHashCode());
        Assert.True(bar200b.Equals((object)bar200a));
        Assert.True(bar200b == bar200a);
        Assert.False(bar200b != bar200a);

        Assert.NotEqual(foo100a, foo200a);
        Assert.False(foo100a.Equals((object)foo200a));
        Assert.False(foo100a == foo200a);
        Assert.True(foo100a != foo200a);
        Assert.NotEqual(foo100a, bar100a);
        Assert.False(foo100a.Equals((object)bar100a));
        Assert.False(foo100a == bar100a);
        Assert.True(foo100a != bar100a);
        Assert.NotEqual(foo100a, bar200a);
        Assert.False(foo100a.Equals((object)bar200a));
        Assert.False(foo100a == bar200a);
        Assert.True(foo100a != bar200a);
        Assert.NotEqual(bar100a, foo200a);
        Assert.False(bar100a.Equals((object)foo200a));
        Assert.False(bar100a == foo200a);
        Assert.True(bar100a != foo200a);
        Assert.NotEqual(foo100a, bar100a);
        Assert.False(foo100a.Equals((object)bar100a));
        Assert.False(foo100a == bar100a);
        Assert.True(foo100a != bar100a);
        Assert.NotEqual(foo100a, bar200a);
        Assert.False(foo100a.Equals((object)bar200a));
        Assert.False(foo100a == bar200a);
        Assert.True(foo100a != bar200a);
        Assert.NotEqual(bar100a, barmax100);
        Assert.False(bar100a.Equals((object)barmax100));
        Assert.False(bar100a == barmax100);
        Assert.True(bar100a != barmax100);

        Assert.False(foo100a.Equals(100));
        Assert.False(foo200a.Equals(200));
    }

    [Fact]
    public void Compare()
    {
        var foo100a = FungibleAssetValue.Create(FOO, 100);
        var foo100b = FungibleAssetValue.Create(FOO, 100);
        var foo200 = FungibleAssetValue.Create(FOO, 200);
        var bar100 = FungibleAssetValue.Create(BAR, 100);
        var barmax100 = FungibleAssetValue.Create(BARMAX, 100);

        Assert.Equal(0, foo100a.CompareTo(foo100b));
        Assert.Equal(0, foo100a.CompareTo((object)foo100b));
        Assert.False(foo100a < foo100b);
        Assert.True(foo100a <= foo100b);
        Assert.False(foo100a > foo100b);
        Assert.True(foo100a >= foo100b);

        Assert.True(foo100a.CompareTo(foo200) < 0);
        Assert.True(foo100a.CompareTo((object)foo200) < 0);
        Assert.True(foo100a < foo200);
        Assert.True(foo100a <= foo200);
        Assert.False(foo100a > foo200);
        Assert.False(foo100a >= foo200);

        Assert.True(foo200.CompareTo(foo100b) > 0);
        Assert.True(foo200.CompareTo((object)foo100b) > 0);
        Assert.False(foo200 < foo100b);
        Assert.False(foo200 <= foo100b);
        Assert.True(foo200 > foo100b);
        Assert.True(foo200 >= foo100b);

        Assert.Throws<ArgumentException>(() => foo100a.CompareTo(bar100));
        Assert.Throws<ArgumentException>(() => foo100a.CompareTo((object)bar100));
        Assert.Throws<ArgumentException>(() => foo100a < bar100);
        Assert.Throws<ArgumentException>(() => foo100a <= bar100);
        Assert.Throws<ArgumentException>(() => foo100a > bar100);
        Assert.Throws<ArgumentException>(() => foo100a >= bar100);

        Assert.Throws<ArgumentException>(() => bar100.CompareTo(barmax100));
        Assert.Throws<ArgumentException>(() => bar100.CompareTo((object)barmax100));
        Assert.Throws<ArgumentException>(() => bar100 < barmax100);
        Assert.Throws<ArgumentException>(() => bar100 <= barmax100);
        Assert.Throws<ArgumentException>(() => bar100 > barmax100);
        Assert.Throws<ArgumentException>(() => bar100 >= barmax100);

        Assert.Throws<ArgumentException>(() => foo100a.CompareTo(100));
    }

    [Fact]
    public void Negate()
    {
        var foo_3 = FungibleAssetValue.Create(FOO, -3);
        var foo0 = FungibleAssetValue.Create(FOO);
        var foo3 = FungibleAssetValue.Create(FOO, 3);

        Assert.Equal(foo_3, -foo3);
        Assert.Equal(foo3, -foo_3);
        Assert.Equal(foo0, -foo0);
    }

    [Fact]
    public void Add()
    {
        var foo_1 = FungibleAssetValue.Create(FOO, -1);
        var foo0 = FungibleAssetValue.Create(FOO);
        var foo1 = FungibleAssetValue.Create(FOO, 1);
        var foo2 = FungibleAssetValue.Create(FOO, 2);
        var foo3 = FungibleAssetValue.Create(FOO, 3);
        var bar3 = FungibleAssetValue.Create(BAR, 3);
        var barmax3 = FungibleAssetValue.Create(BARMAX, 3);

        Assert.Equal(foo1, foo1 + foo0);
        Assert.Equal(foo1, foo0 + foo1);
        Assert.Equal(foo2, foo1 + foo1);
        Assert.Equal(foo3, foo1 + foo2);
        Assert.Equal(foo3, foo2 + foo1);
        Assert.Equal(foo1, foo2 + foo_1);
        Assert.Equal(foo1, foo_1 + foo2);
        Assert.Equal(foo_1, foo_1 + foo0);
        Assert.Equal(foo_1, foo0 + foo_1);

        Assert.Throws<ArgumentException>(() => foo1 + bar3);
        Assert.Throws<ArgumentException>(() => bar3 + barmax3);
    }

    [Fact]
    public void Subtract()
    {
        var foo_1 = FungibleAssetValue.Create(FOO, -1);
        var foo0 = FungibleAssetValue.Create(FOO);
        var foo1 = FungibleAssetValue.Create(FOO, 1);
        var foo2 = FungibleAssetValue.Create(FOO, 2);
        var bar3 = FungibleAssetValue.Create(BAR, 3);
        var barmax3 = FungibleAssetValue.Create(BARMAX, 3);

        Assert.Equal(foo0, foo1 - foo1);
        Assert.Equal(foo_1, foo1 - foo2);
        Assert.Equal(foo2, foo1 - foo_1);
        Assert.Equal(foo0, foo_1 - foo_1);

        Assert.Throws<ArgumentException>(() => bar3 - foo1);
        Assert.Throws<ArgumentException>(() => bar3 - barmax3);
    }

    [Fact]
    public void Multiply()
    {
        var foo_2 = FungibleAssetValue.Create(FOO, -2);
        var foo_1 = FungibleAssetValue.Create(FOO, -1);
        var foo0 = FungibleAssetValue.Create(FOO);
        var foo1 = FungibleAssetValue.Create(FOO, 1);
        var foo2 = FungibleAssetValue.Create(FOO, 2);
        var foo4 = FungibleAssetValue.Create(FOO, 4);

        Assert.Equal(foo2, foo1 * 2);
        Assert.Equal(foo2, 2 * foo1);
        Assert.Equal(foo2, foo2 * 1);
        Assert.Equal(foo2, 1 * foo2);
        Assert.Equal(foo_2, foo2 * -1);
        Assert.Equal(foo_2, -1 * foo2);
        Assert.Equal(foo_2, foo_1 * 2);
        Assert.Equal(foo_2, 2 * foo_1);
        Assert.Equal(foo_1, foo_1 * 1);
        Assert.Equal(foo_1, 1 * foo_1);
        Assert.Equal(foo4, foo2 * 2);
        Assert.Equal(foo4, 2 * foo2);
        Assert.Equal(foo0, foo2 * 0);
        Assert.Equal(foo0, 0 * foo2);
        Assert.Equal(foo0, foo_1 * 0);
        Assert.Equal(foo0, 0 * foo_1);
    }

    [Fact]
    public void DivRem()
    {
        var foo7 = FungibleAssetValue.Create(FOO, 7);
        var foo6 = FungibleAssetValue.Create(FOO, 6);
        var foo3 = FungibleAssetValue.Create(FOO, 3);
        var foo2 = FungibleAssetValue.Create(FOO, 2);
        var foo1 = FungibleAssetValue.Create(FOO, 1);
        var foo0 = FungibleAssetValue.Create(FOO);
        FungibleAssetValue rem;

        Assert.Equal((foo6, foo0), foo6.DivRem(1));
        Assert.Equal(foo6, foo6.DivRem(1, out rem));
        Assert.Equal(foo0, rem);
        Assert.Equal(foo0, foo6 % 1);

        Assert.Equal((foo2, foo0), foo6.DivRem(3));
        Assert.Equal(foo2, foo6.DivRem(3, out rem));
        Assert.Equal(foo0, rem);
        Assert.Equal(foo0, foo6 % 3);

        Assert.Equal((foo2, foo1), foo7.DivRem(3));
        Assert.Equal(foo2, foo7.DivRem(3, out rem));
        Assert.Equal(foo1, rem);
        Assert.Equal(foo1, foo7 % 3);

        Assert.Equal((foo0, foo6), foo6.DivRem(7));
        Assert.Equal(foo0, foo6.DivRem(7, out rem));
        Assert.Equal(foo6, rem);
        Assert.Equal(foo6, foo6 % 7);

        Assert.Equal((foo0, foo0), foo0.DivRem(2));
        Assert.Equal(foo0, foo0.DivRem(2, out rem));
        Assert.Equal(foo0, rem);
        Assert.Equal(foo0, foo0 % 2);

        Assert.Equal((6, foo0), foo6.DivRem(foo1));
        Assert.Equal(6, foo6.DivRem(foo1, out rem));
        Assert.Equal(foo0, rem);
        Assert.Equal(foo0, foo6 % foo1);

        Assert.Equal((2, foo0), foo6.DivRem(foo3));
        Assert.Equal(2, foo6.DivRem(foo3, out rem));
        Assert.Equal(foo0, rem);
        Assert.Equal(foo0, foo6 % foo3);

        Assert.Equal((2, foo1), foo7.DivRem(foo3));
        Assert.Equal(2, foo7.DivRem(foo3, out rem));
        Assert.Equal(foo1, rem);
        Assert.Equal(foo1, foo7 % foo3);

        Assert.Equal((0, foo6), foo6.DivRem(foo7));
        Assert.Equal(0, foo6.DivRem(foo7, out rem));
        Assert.Equal(foo6, rem);
        Assert.Equal(foo6, foo6 % foo7);

        Assert.Equal((0, foo0), foo0.DivRem(foo2));
        Assert.Equal(0, foo0.DivRem(foo2, out rem));
        Assert.Equal(foo0, rem);
        Assert.Equal(foo0, foo0 % foo2);

        Assert.Throws<DivideByZeroException>(() => foo1.DivRem(0));
        Assert.Throws<DivideByZeroException>(() => foo1.DivRem(0, out rem));
        Assert.Throws<DivideByZeroException>(() => foo1 % 0);
        Assert.Throws<DivideByZeroException>(() => foo1.DivRem(foo0));
        Assert.Throws<DivideByZeroException>(() => foo1.DivRem(foo0, out rem));
        Assert.Throws<DivideByZeroException>(() => foo1 % foo0);

        var bar1 = FungibleAssetValue.Create(BAR, 1);
        Assert.Throws<ArgumentException>(() => bar1.DivRem(foo1));
        Assert.Throws<ArgumentException>(() => bar1.DivRem(foo1, out rem));
        Assert.Throws<ArgumentException>(() => bar1 % foo1);
    }

    [Fact]
    public void Abs()
    {
        var foo_3 = FungibleAssetValue.Create(FOO, -3);
        var foo0 = FungibleAssetValue.Create(FOO);
        var foo3 = FungibleAssetValue.Create(FOO, 3);

        Assert.Equal(foo3, FungibleAssetValue.Abs(foo3));
        Assert.Equal(foo3, FungibleAssetValue.Abs(foo_3));
        Assert.Equal(foo0, FungibleAssetValue.Abs(foo0));
    }

    [Fact]
    public void GetQuantityString()
    {
        FungibleAssetValue v;
        v = FungibleAssetValue.Create(FOO, 123, 45);
        Assert.Equal("123.45", v.GetQuantityString());
        Assert.Equal("123.45", v.GetQuantityString(true));

        v = FungibleAssetValue.Create(FOO, 456, 9);
        Assert.Equal("456.09", v.GetQuantityString());
        Assert.Equal("456.09", v.GetQuantityString(true));

        v = FungibleAssetValue.Create(FOO, 0, 10);
        Assert.Equal("0.1", v.GetQuantityString());
        Assert.Equal("0.10", v.GetQuantityString(true));

        v = FungibleAssetValue.Create(FOO, 0, 9);
        Assert.Equal("0.09", v.GetQuantityString());
        Assert.Equal("0.09", v.GetQuantityString(true));

        v = FungibleAssetValue.Create(FOO, -789, -1);
        Assert.Equal("-789.01", v.GetQuantityString());
        Assert.Equal("-789.01", v.GetQuantityString(true));

        v = FungibleAssetValue.Create(FOO, 0, -2);
        Assert.Equal("-0.02", v.GetQuantityString());
        Assert.Equal("-0.02", v.GetQuantityString(true));

        v = FungibleAssetValue.Create(FOO, 123, 0);
        Assert.Equal("123", v.GetQuantityString());
        Assert.Equal("123.00", v.GetQuantityString(true));

        v = FungibleAssetValue.Create(FOO, 0, 0);
        Assert.Equal("0", v.GetQuantityString());
        Assert.Equal("0.00", v.GetQuantityString(true));
    }

    [Fact]
    public void String()
    {
        var foo100 = FungibleAssetValue.Create(FOO, 100);
        var bar90000000 = FungibleAssetValue.Create(BAR, 90000000);
        Assert.Equal("1 FOO", foo100.ToString());
        Assert.Equal("90000000 BAR", bar90000000.ToString());
    }

    [Fact]
    public void Parse()
    {
        var baz = Currency.Create("BAZ", 1);

        Assert.Throws<FormatException>(() => FungibleAssetValue.Parse(FOO, "abc"));

        Assert.Throws<FormatException>(() => FungibleAssetValue.Parse(FOO, "++123"));
        Assert.Throws<FormatException>(() => FungibleAssetValue.Parse(FOO, "--123"));
        Assert.Throws<FormatException>(() => FungibleAssetValue.Parse(FOO, "++123.45"));
        Assert.Throws<FormatException>(() => FungibleAssetValue.Parse(FOO, "--123.45"));
        Assert.Throws<FormatException>(() => FungibleAssetValue.Parse(FOO, "23.4-5"));
        Assert.Throws<FormatException>(() => FungibleAssetValue.Parse(FOO, "45.6+7"));
        Assert.Throws<FormatException>(() => FungibleAssetValue.Parse(FOO, "2-3"));
        Assert.Throws<FormatException>(() => FungibleAssetValue.Parse(FOO, "45+6"));
        Assert.Throws<FormatException>(() => FungibleAssetValue.Parse(FOO, "+12-3"));
        Assert.Throws<FormatException>(() => FungibleAssetValue.Parse(FOO, "-45+6"));

        Assert.Throws<FormatException>(() => FungibleAssetValue.Parse(FOO, "123..4"));
        Assert.Throws<FormatException>(() => FungibleAssetValue.Parse(FOO, "123.4.5"));

        Assert.Throws<FormatException>(() => FungibleAssetValue.Parse(FOO, "123.456"));
        Assert.Throws<FormatException>(() => FungibleAssetValue.Parse(BAR, "123.0"));
        Assert.Throws<FormatException>(() => FungibleAssetValue.Parse(baz, "123.12"));

        Assert.Equal(
            FungibleAssetValue.Create(FOO, 123, 45),
            FungibleAssetValue.Parse(FOO, "123.45")
        );
        Assert.Equal(
            FungibleAssetValue.Create(FOO, 123, 45),
            FungibleAssetValue.Parse(FOO, "+123.45")
        );
        Assert.Equal(
            FungibleAssetValue.Create(FOO, -123, -45),
            FungibleAssetValue.Parse(FOO, "-123.45")
        );
        Assert.Equal(
            FungibleAssetValue.Create(FOO, 123, 40),
            FungibleAssetValue.Parse(FOO, "123.4")
        );
        Assert.Equal(
            FungibleAssetValue.Create(FOO, 123, 40),
            FungibleAssetValue.Parse(FOO, "+123.4")
        );
        Assert.Equal(
            FungibleAssetValue.Create(FOO, -123, -40),
            FungibleAssetValue.Parse(FOO, "-123.4")
        );
        Assert.Equal(FungibleAssetValue.Create(FOO, 123, 0), FungibleAssetValue.Parse(FOO, "123"));
        Assert.Equal(FungibleAssetValue.Create(FOO, 12, 0), FungibleAssetValue.Parse(FOO, "+12"));
        Assert.Equal(FungibleAssetValue.Create(FOO, -12, 0), FungibleAssetValue.Parse(FOO, "-12"));
    }

    [SkippableFact]
    public void JsonSerialization()
    {
        var v = FungibleAssetValue.Create(FOO, 123, 45);
        AssertJsonSerializable(v, @"
            {
                ""quantity"": ""123.45"",
                ""currency"": {
                    ""hash"": ""ea0ec4314a6124d97b42c8f9d15e961030c4f57b"",
                    ""ticker"": ""FOO"",
                    ""decimalPlaces"": 2,
                    ""minters"": [],
                    ""maximumSupply"": ""0""
                }
            }
        ");

        v = FungibleAssetValue.Create(FOO, -456, 0);
        AssertJsonSerializable(v, @"
            {
                ""quantity"": ""-456"",
                ""currency"": {
                    ""hash"": ""ea0ec4314a6124d97b42c8f9d15e961030c4f57b"",
                    ""ticker"": ""FOO"",
                    ""decimalPlaces"": 2,
                    ""minters"": [],
                    ""maximumSupply"": ""0""
                }
            }
        ");
    }
}

#pragma warning restore S1764
