using Libplanet.Types;

namespace Libplanet.Net.Tests.Protocols;

public class AddressUtilityTest
{
    [Fact]
    public void GetDifference()
    {
        var address1 = Address.Parse("000000000000000000000000000000000000000c");
        var address2 = Address.Parse("0000000001000001111110001000011001000001");

        Assert.Equal(
            Address.Parse("000000000100000111111000100001100100000d"),
            AddressUtility.GetDifference(address1, address2));
    }

    [Fact]
    public void CommonPrefixLength()
    {
        var address1 = Address.Parse("0000000000000000000000000000000000000000");
        var address2 = Address.Parse("0000000000000000000000000000000000000001");
        var address3 = Address.Parse("000000000000000000000000000000000000000c");
        var address4 = Address.Parse("0000000001000001111110001000011001000001");

        Assert.Equal(159, AddressUtility.CommonPrefixLength(address1, address2));
        Assert.Equal(156, AddressUtility.CommonPrefixLength(address1, address3));
        Assert.Equal(39, AddressUtility.CommonPrefixLength(address1, address4));
    }

    [Fact]
    public void GetDistance()
    {
        var address1 = Address.Parse("0000000000000000000000000000000000000000");
        var address2 = Address.Parse("0000000001000001111110001000011001000001");
        var address3 = Address.Parse("ffffffffffffffffffffffffffffffffffffffff");
        Assert.Equal(121, AddressUtility.GetDistance(address1, address2));
        Assert.Equal(0, AddressUtility.GetDistance(address2, address2));
        Assert.Equal(Address.Size * 8, AddressUtility.GetDistance(address1, address3));
    }

    [Fact]
    public void CompareOrdinal()
    {
        var address1 = Address.Parse("0000000000000000000000000000000000000000");
        var address2 = Address.Parse("0000000000000000000000000000000000000001");
        var address3 = Address.Parse("000000000000000000000000000000000000000c");
        var address4 = Address.Parse("0000000001000001111110001000011001000001");

        Assert.True(string.CompareOrdinal($"{address1:raw}", $"{address2:raw}") < 1);
        Assert.True(string.CompareOrdinal($"{address2:raw}", $"{address3:raw}") < 1);
        Assert.True(string.CompareOrdinal($"{address3:raw}", $"{address4:raw}") < 1);
        Assert.Equal(0, string.CompareOrdinal($"{address4:raw}", $"{address4:raw}"));
    }
}
