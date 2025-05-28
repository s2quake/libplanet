using System.ComponentModel;
using Libplanet.Serialization;
using Libplanet.Types;
using Libplanet.Types.Tests;
using static Libplanet.Tests.TestUtils;

namespace Libplanet.Tests;

public class AddressTest
{
    [Fact]
    public void ConstructWithImmutableArray()
    {
        byte[] addr =
        [
            0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef, 0xab,
            0xcd, 0xef, 0xab, 0xcd, 0xef, 0xab, 0xcd, 0xef, 0xab,
            0xcd, 0xef,
        ];

        Assert.Equal(Address.Parse("0123456789ABcdefABcdEfABcdEFabcDEFabCDEF"), new Address(addr));
        Assert.Equal(Address.Parse("0123456789ABcdefABcdEfABcdEFabcDEFabCDEF"), new Address(addr.ToImmutableArray()));
    }

    [Fact]
    public void DefaultConstructor()
    {
        Address defaultValue = default;
        Assert.Equal(new Address(new byte[20]), defaultValue);
    }

    [Fact]
    public void DerivingConstructor()
    {
        var publicKey = new PublicKey(ImmutableArray.Create<byte>(
        [
            0x03, 0x43, 0x8b, 0x93, 0x53, 0x89, 0xa7, 0xeb, 0xf8,
            0x38, 0xb3, 0xae, 0x41, 0x25, 0xbd, 0x28, 0x50, 0x6a,
            0xa2, 0xdd, 0x45, 0x7f, 0x20, 0xaf, 0xc8, 0x43, 0x72,
            0x9d, 0x3e, 0x7d, 0x60, 0xd7, 0x28,
        ]));
        var expectedAddress = new Address(ImmutableArray.Create<byte>(
        [
            0xd4, 0x1f, 0xad, 0xf6, 0x1b, 0xad, 0xf5, 0xbe,
            0x2d, 0xe6, 0x0e, 0x9f, 0xc3, 0x23, 0x0c, 0x0a,
            0x8a, 0x43, 0x90, 0xf0,
        ]));

        Assert.Equal(expectedAddress, new Address(publicKey));
    }

    [Fact]
    public void HexAddressConstructor()
    {
        Assert.Equal(
            new Address(
            [
                0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef, 0xab,
                0xcd, 0xef, 0xab, 0xcd, 0xef, 0xab, 0xcd, 0xef, 0xab,
                0xcd, 0xef,
            ]),
            Address.Parse("0123456789ABcdefABcdEfABcdEFabcDEFabCDEF"));

        var address = Address.Parse("45a22187e2d8850bb357886958bc3e8560929ccc");
        Assert.Equal("45a22187e2D8850bb357886958bC3E8560929ccc", address.ToString("raw", null));
    }

    [Fact]
    public void DeriveFromHex()
    {
        Assert.Throws<FormatException>(() => Address.Parse("0123456789ABcdefABcdEfABcdEFabcDEFabCDE"));
        Assert.Throws<FormatException>(() => Address.Parse("0123456789ABcdefABcdEfABcdEFabcDEFabCDEFF"));
        Assert.Throws<FormatException>(() => Address.Parse("1x0123456789ABcdefABcdEfABcdEFabcDEFabCDEF"));
        Assert.Throws<FormatException>(() => Address.Parse("0x0123456789ABcdefABcdEfABcdEFabcDEFabCDE"));
        Assert.Throws<FormatException>(() => Address.Parse("0x0123456789ABcdefABcdEfABcdEFabcDEFabCDEFF"));
    }

    [Fact]
    public void HexAddressConstructorOnlyTakesHexadecimalCharacters()
    {
        Assert.Throws<FormatException>(() => Address.Parse("45a22187e2d8850bb357886958BC3E8560929ghi"));
        Assert.Throws<FormatException>(() => Address.Parse("45a22187e2d8850bb357886958BC3E8560929£한글"));
    }

    [Fact]
    public void CanDetectInvalidMixedCaseChecksum()
    {
        Assert.Throws<FormatException>(() => Address.Parse("45A22187E2D8850BB357886958BC3E8560929CCC"));
    }

    [Fact]
    public void AddressMustBe20Bytes()
    {
        for (var size = 0; size < 25; size++)
        {
            if (size == 20)
            {
                continue;
            }

            var bytes = RandomUtility.Array(RandomUtility.Byte, size);
            Assert.Throws<ArgumentException>(() => new Address(bytes));
        }
    }

    [Fact]
    public void ToByteArray()
    {
        var addressBytes = GetRandomBytes(20);
        var address = new Address([.. addressBytes]);
        Assert.Equal(addressBytes, address.ToByteArray());
    }

    [Fact]
    public void ToByteArrayShouldNotExposeContents()
    {
        var address = new Address(
        [
            0x45, 0xa2, 0x21, 0x87, 0xe2, 0xd8, 0x85, 0x0b, 0xb3, 0x57,
            0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
        ]);
        address.ToByteArray()[0] = 0x00;

        Assert.Equal(0x45, address.ToByteArray()[0]);
    }

    [Fact]
    public void ToHex()
    {
        var address = new Address(
        [
            0x45, 0xa2, 0x21, 0x87, 0xe2, 0xd8, 0x85, 0x0b, 0xb3, 0x57,
            0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
        ]);
        Assert.Equal("45a22187e2D8850bb357886958bC3E8560929ccc", address.ToString("raw", null));
        Assert.Equal("0x45a22187e2D8850bb357886958bC3E8560929ccc", address.ToString());
    }

    [Fact]
    public void Equals_()
    {
        var sameAddress1 = new Address(
        [
            0x45, 0xa2, 0x21, 0x87, 0xe2, 0xd8, 0x85, 0x0b, 0xb3, 0x57,
            0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
        ]);
        var sameAddress2 = new Address(
        [
            0x45, 0xa2, 0x21, 0x87, 0xe2, 0xd8, 0x85, 0x0b, 0xb3, 0x57,
            0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
        ]);
        var differentAddress = new Address(
        [
            0x45, 0xa2, 0x21, 0x87, 0xe2, 0xd8, 0x85, 0x0b, 0xb3, 0x57,
            0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0x00,
        ]);

        Assert.Equal(sameAddress1, sameAddress2);
        Assert.NotEqual(sameAddress2, differentAddress);

        Assert.True(sameAddress1 == sameAddress2);
        Assert.False(sameAddress2 == differentAddress);

        Assert.False(sameAddress1 != sameAddress2);
        Assert.True(sameAddress2 != differentAddress);
    }

    [Fact]
    public void SerializeAndDeserializeWithDefault()
    {
        var defaultAddress = default(Address);
        Address deserializedAddress = ModelSerializer.Clone(defaultAddress);
        Assert.Equal(default, deserializedAddress);
    }

    [Fact]
    public void Compare()
    {
        var addresses = RandomUtility.Array(RandomUtility.Address, 50);

        for (var i = 1; i < addresses.Length; i++)
        {
            var left = addresses[i - 1];
            var right = addresses[i];
            var leftString = addresses[i - 1].ToString("raw", null).ToLower();
            var rightString = right.ToString("raw", null).ToLower();
            Assert.Equal(
                Math.Min(Math.Max(left.CompareTo(right), 1), -1),
                Math.Min(Math.Max(leftString.CompareTo(rightString), 1), -1));
            Assert.Equal(
                left.CompareTo(right),
                left.CompareTo(right as object));
        }

        Assert.Equal(1, addresses[0].CompareTo(null));
        Assert.Throws<ArgumentException>(() => addresses[0].CompareTo("invalid"));
    }

    [Fact]
    public void ReplaceHexPrefixString()
    {
        var address = Address.Parse("0x0123456789ABcdefABcdEfABcdEFabcDEFabCDEF");
        Assert.Equal("0x0123456789ABcdefABcdEfABcdEFabcDEFabCDEF", address.ToString());
    }

    [Fact]
    public void ReplaceHexUpperCasePrefixString()
    {
        Assert.Throws<FormatException>(() => Address.Parse("0X0123456789ABcdefABcdEfABcdEFabcDEFabCDEF"));
    }

    [Fact]
    public void Bencoded()
    {
        var expected = RandomUtility.Address();
        var deserialized = ModelSerializer.Clone(expected);
        Assert.Equal(expected, deserialized);
        expected = default;
        deserialized = ModelSerializer.Clone(expected);
        Assert.Equal(expected, deserialized);
    }

    [Fact]
    public void TypeConverter()
    {
        var converter = TypeDescriptor.GetConverter(typeof(Address));
        var address = Address.Parse("0123456789ABcdefABcdEfABcdEFabcDEFabCDEF");
        Assert.True(converter.CanConvertFrom(typeof(string)));
        Assert.Equal(address, converter.ConvertFrom("0x0123456789ABcdefABcdEfABcdEFabcDEFabCDEF"));
        Assert.Equal(address, converter.ConvertFrom("0123456789ABcdefABcdEfABcdEFabcDEFabCDEF"));
        Assert.Throws<FormatException>(() => converter.ConvertFrom("INVALID"));
        Assert.True(converter.CanConvertTo(typeof(string)));
        Assert.Equal("0123456789ABcdefABcdEfABcdEFabcDEFabCDEF", converter.ConvertTo(address, typeof(string)));
    }

    [Fact]
    public void JsonSerialization()
    {
        var address = Address.Parse("0123456789ABcdefABcdEfABcdEFabcDEFabCDEF");
        AssertJsonSerializable(address, "\"0123456789ABcdefABcdEfABcdEFabcDEFabCDEF\"");
    }
}
