using System.Reflection;
using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.TestUtilities;
using Xunit.Abstractions;

namespace Libplanet.Types.Tests;

public sealed partial class BlockHashTest(ITestOutputHelper output)
{
    [Fact]
    public void Attribute()
    {
        var attribute = typeof(BlockHash).GetCustomAttribute<ModelConverterAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal("blhs", attribute.TypeName);
    }

    [Fact]
    public void SerializeAndDeserialize()
    {
        var random = RandomUtility.GetRandom(output);
        var expected = RandomUtility.BlockHash(random);
        var serialized = ModelSerializer.SerializeToBytes(expected);
        var actual = ModelSerializer.DeserializeFromBytes(serialized);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Ctor()
    {
        var random = RandomUtility.GetRandom(output);
        var bytes = RandomUtility.Bytes(random, BlockHash.Size);
        var hex = ByteUtility.Hex(bytes);

        Assert.Equal(BlockHash.Parse(hex), new BlockHash(bytes));
        Assert.Equal(BlockHash.Parse(hex), new BlockHash(bytes.ToImmutableArray()));
    }

    [Fact]
    public void Ctor_Throw()
    {
        var random = RandomUtility.GetRandom(output);
        var bytes1 = RandomUtility.Bytes(random, BlockHash.Size + 1);
        var bytes2 = RandomUtility.Bytes(random, BlockHash.Size - 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => new BlockHash(bytes1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BlockHash(bytes2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BlockHash([]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BlockHash(bytes1.ToImmutableArray()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BlockHash(bytes2.ToImmutableArray()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BlockHash(ImmutableArray<byte>.Empty));
    }

    [Fact]
    public void Default()
    {
        BlockHash defaultValue = default;
        Assert.Equal(new BlockHash(new byte[32]), defaultValue);
    }

    [Fact]
    public void Bytes()
    {
        var random = RandomUtility.GetRandom(output);
        var bytes = RandomUtility.Array(random, RandomUtility.Byte, BlockHash.Size);
        var address = new BlockHash(bytes);
        Assert.Equal(bytes, address.Bytes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("string")]
    [InlineData("£")]
    [InlineData("한글")]
    public void Parse(string hex)
    {
        Assert.Throws<FormatException>(() => BlockHash.Parse(hex));
    }

    [Fact]
    public void Parse_WithInvalidLength()
    {
        var random = RandomUtility.GetRandom(output);
        var hex1 = RandomUtility.Hex(random, BlockHash.Size - 1);
        var hex2 = RandomUtility.Hex(random, BlockHash.Size + 1);
        Assert.Throws<FormatException>(() => BlockHash.Parse(hex1));
        Assert.Throws<FormatException>(() => BlockHash.Parse(hex2));
    }

    [Fact]
    public void HashData()
    {
        var random = RandomUtility.GetRandom(output);
        var bytes = RandomUtility.Bytes(random, 100);
        var expectedHash = SHA256.HashData(bytes);
        var blockHash = BlockHash.HashData(bytes);

        Assert.Equal(expectedHash, blockHash.Bytes);
    }

    [Fact]
    public void Equals_Test()
    {
        var random = RandomUtility.GetRandom(output);
        var blockHash1 = RandomUtility.BlockHash(random);
        var blockHash2 = RandomUtility.BlockHash(random);
        var sameBlockHash = new BlockHash(blockHash1.Bytes);

        Assert.True(blockHash1.Equals(sameBlockHash));
        Assert.False(blockHash1.Equals(blockHash2));
        Assert.False(blockHash1.Equals(null));
        Assert.False(blockHash1.Equals("not an address"));
    }

    [Fact]
    public void GetHashCode_Test()
    {
        var random = RandomUtility.GetRandom(output);
        var blockHash1 = RandomUtility.BlockHash(random);
        var blockHash2 = RandomUtility.BlockHash(random);
        var sameBlockHash = new BlockHash(blockHash1.Bytes);

        Assert.Equal(blockHash1.GetHashCode(), ByteUtility.GetHashCode(blockHash1.Bytes));
        Assert.Equal(blockHash1.GetHashCode(), sameBlockHash.GetHashCode());
        Assert.NotEqual(blockHash1.GetHashCode(), blockHash2.GetHashCode());
    }

    [Fact]
    public void ToString_Test()
    {
        var random = RandomUtility.GetRandom(output);
        var hex = RandomUtility.Hex(random, BlockHash.Size);
        var blockHash = BlockHash.Parse(hex);
        Assert.Equal(hex, blockHash.ToString());
        Assert.Equal(hex, blockHash.ToString(null, null));
        Assert.Equal(hex, blockHash.ToString("h", null));
        Assert.Equal(hex.ToUpper(), blockHash.ToString("H", null));
        Assert.Throws<FormatException>(() => blockHash.ToString("q", null));
    }

    [Fact]
    public void CompareTo_Test()
    {
        var blockHash1 = BlockHash.Parse("982db4060b0b7fc3cdbae05949a7b714d3292a4c45c004982ebcab0373162e92");
        var blockHash2 = BlockHash.Parse("982db4060b0b7fc3cdbae05949a7b714d3292a4c45c004982ebcab0373162e93");
        Assert.True(blockHash1.CompareTo(blockHash2) < 0);
        Assert.True(blockHash2.CompareTo(blockHash1) > 0);
        Assert.Equal(0, blockHash1.CompareTo(blockHash1));
        Assert.Equal(1, blockHash1.CompareTo(null));
        Assert.Throws<ArgumentException>(() => blockHash1.CompareTo("not an address"));
    }
}
