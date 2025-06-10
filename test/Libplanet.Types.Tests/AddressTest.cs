using System.Reflection;
using Libplanet.Serialization;
using Libplanet.TestUtilities;
using Xunit.Abstractions;

namespace Libplanet.Types.Tests;

public sealed partial class AddressTest(ITestOutputHelper output)
{
    [Fact]
    public void Attribute()
    {
        var attribute = typeof(Address).GetCustomAttribute<ModelConverterAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal("addr", attribute.TypeName);
    }

    [Fact]
    public void SerializeAndDeserialize()
    {
        var random = RandomUtility.GetRandom(output);
        var address1 = RandomUtility.Address(random);
        var serialized = ModelSerializer.SerializeToBytes(address1);
        var address2 = ModelSerializer.DeserializeFromBytes(serialized);
        Assert.Equal(address1, address2);
    }

    [Fact]
    public void Ctor()
    {
        byte[] bytes =
        [
            0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef, 0xab,
            0xcd, 0xef, 0xab, 0xcd, 0xef, 0xab, 0xcd, 0xef, 0xab,
            0xcd, 0xef,
        ];

        Assert.Equal(Address.Parse("0123456789ABcdefABcdEfABcdEFabcDEFabCDEF"), new Address(bytes));
        Assert.Equal(Address.Parse("0123456789ABcdefABcdEfABcdEFabcDEFabCDEF"), new Address(bytes.ToImmutableArray()));
    }

    [Fact]
    public void Default()
    {
        Address defaultValue = default;
        Assert.Equal(new Address(new byte[20]), defaultValue);
    }

    [Fact]
    public void Ctor_WithPublicKey()
    {
        var publicKey = new PublicKey(
        [
            0x03, 0x43, 0x8b, 0x93, 0x53, 0x89, 0xa7, 0xeb, 0xf8,
            0x38, 0xb3, 0xae, 0x41, 0x25, 0xbd, 0x28, 0x50, 0x6a,
            0xa2, 0xdd, 0x45, 0x7f, 0x20, 0xaf, 0xc8, 0x43, 0x72,
            0x9d, 0x3e, 0x7d, 0x60, 0xd7, 0x28,
        ]);
        var expectedAddress = new Address(
        [
            0xd4, 0x1f, 0xad, 0xf6, 0x1b, 0xad, 0xf5, 0xbe,
            0x2d, 0xe6, 0x0e, 0x9f, 0xc3, 0x23, 0x0c, 0x0a,
            0x8a, 0x43, 0x90, 0xf0,
        ]);

        Assert.Equal(expectedAddress, new Address(publicKey));
    }

    [Fact]
    public void Bytes()
    {
        var random = RandomUtility.GetRandom(output);
        var bytes = RandomUtility.Array(random, RandomUtility.Byte, Address.Size);
        var address = new Address(bytes);
        Assert.Equal(bytes, address.Bytes);
    }

    [Theory]
    [InlineData("0123456789ABcdefABcdEfABcdEFabcDEFabCDE")]
    [InlineData("0123456789ABcdefABcdEfABcdEFabcDEFabCDEFF")]
    [InlineData("1x0123456789ABcdefABcdEfABcdEFabcDEFabCDEF")]
    [InlineData("0x0123456789ABcdefABcdEfABcdEFabcDEFabCDE")]
    [InlineData("0x0123456789ABcdefABcdEfABcdEFabcDEFabCDEFF")]
    [InlineData("45a22187e2d8850bb357886958BC3E8560929ghi")]
    [InlineData("45a22187e2d8850bb357886958BC3E8560929£한글")]
    [InlineData("45A22187E2D8850BB357886958BC3E8560929CCC")]
    public void Parse(string hex)
    {
        Assert.Throws<FormatException>(() => Address.Parse(hex));
    }

    [Fact]
    public void Verify()
    {
        var random = RandomUtility.GetRandom(output);
        var privateKey = RandomUtility.PrivateKey(random);
        var address = privateKey.Address;
        var message = RandomUtility.Array(random, RandomUtility.Byte);
        var signature = privateKey.Sign(message);
        Assert.True(address.Verify(message, signature));

        var invalidAddress = RandomUtility.Address(random);
        Assert.False(invalidAddress.Verify(message, signature));

        var invalidSignature = RandomUtility.Array(random, RandomUtility.Byte, signature.Length);
        Assert.False(address.Verify(message, invalidSignature));
        Assert.False(address.Verify(message, []));
    }

    [Fact]
    public void Equals_Test()
    {
        var random = RandomUtility.GetRandom(output);
        var address1 = RandomUtility.Address(random);
        var address2 = RandomUtility.Address(random);
        var sameAddress = new Address(address1.Bytes);

        Assert.True(address1.Equals(sameAddress));
        Assert.False(address1.Equals(address2));
        Assert.False(address1.Equals(null));
        Assert.False(address1.Equals("not an address"));
    }

    [Fact]
    public void GetHashCode_Test()
    {
        var random = RandomUtility.GetRandom(output);
        var address1 = RandomUtility.Address(random);
        var address2 = RandomUtility.Address(random);
        var sameAddress = new Address(address1.Bytes);

        Assert.Equal(address1.GetHashCode(), ByteUtility.GetHashCode(address1.Bytes));
        Assert.Equal(address1.GetHashCode(), sameAddress.GetHashCode());
        Assert.NotEqual(address1.GetHashCode(), address2.GetHashCode());
    }

    [Fact]
    public void ToString_Test()
    {
        var address = Address.Parse("04ec8128E2024865f78e13622F1396e117912D04");
        Assert.Equal("0x04ec8128E2024865f78e13622F1396e117912D04", address.ToString());
        Assert.Equal("0x04ec8128E2024865f78e13622F1396e117912D04", address.ToString(null, null));
        Assert.Equal("04ec8128E2024865f78e13622F1396e117912D04", address.ToString("raw", null));
    }

    [Fact]
    public void CompareTo_Test()
    {
        var address1 = Address.Parse("0000000000000000000000000000000000000000");
        var address2 = Address.Parse("0000000000000000000000000000000000000001");
        Assert.True(address1.CompareTo(address2) < 0);
        Assert.True(address2.CompareTo(address1) > 0);
        Assert.Equal(0, address1.CompareTo(address1));
        Assert.Equal(1, address1.CompareTo(null));
        Assert.Throws<ArgumentException>(() => address1.CompareTo("not an address"));
    }
}
