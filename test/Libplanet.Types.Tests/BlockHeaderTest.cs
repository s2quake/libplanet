using System.Reflection;
using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.TestUtilities;
using Libplanet.Serialization.Tests;

namespace Libplanet.Types.Tests;

public sealed class BlockHeaderTest(ITestOutputHelper output)
{
    [Fact]
    public void Attribute()
    {
        var attribute = typeof(BlockHeader).GetCustomAttribute<ModelAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal(1, attribute.Version);
        Assert.Equal("blkhd", attribute.TypeName);
    }

    [Fact]
    public void SerializeAndDeserialize()
    {
        var random = Rand.GetRandom(output);
        var expected = Rand.BlockHeader(random);
        var serialized = ModelSerializer.SerializeToBytes(expected);
        var actual = ModelSerializer.DeserializeFromBytes(serialized);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Ctor()
    {
        var proposer = Rand.Address();
        var blockHeader = new BlockHeader
        {
            Height = 0,
            Timestamp = DateTimeOffset.UtcNow,
            Proposer = proposer,
        };
        Assert.Equal(BlockHeader.CurrentVersion, blockHeader.Version);
        Assert.Equal(0, blockHeader.Height);
        Assert.True(blockHeader.Timestamp < DateTimeOffset.UtcNow);
        Assert.Equal(proposer, blockHeader.Proposer);
        Assert.Equal(default, blockHeader.PreviousBlockHash);
        Assert.Equal(default, blockHeader.PreviousBlockCommit);
        Assert.Equal(default, blockHeader.PreviousStateRootHash);
    }

    [Fact]
    public void Version()
    {
        var random = Rand.GetRandom(output);
        var blockHeader = Rand.BlockHeader(random) with
        {
            Version = 0,
        };
        Assert.Equal(0, blockHeader.Version);

        ModelAssert.Throws(blockHeader with
        {
            Version = BlockHeader.CurrentVersion + 1,
        }, nameof(BlockHeader.Version));

        ModelAssert.Throws(blockHeader with
        {
            Version = -1,
        }, nameof(BlockHeader.Version));
    }

    [Fact]
    public void Height()
    {
        var random = Rand.GetRandom(output);
        var height = Rand.NonNegative(random);
        var blockHeader = Rand.BlockHeader(random) with
        {
            Height = height,
        };
        Assert.Equal(height, blockHeader.Height);

        ModelAssert.Throws(blockHeader with
        {
            Height = -1,
        }, nameof(BlockHeader.Height));
    }

    [Fact]
    public void Timestamp()
    {
        var random = Rand.GetRandom(output);
        var timestamp = Rand.DateTimeOffset(random);
        var blockHeader = Rand.BlockHeader(random) with
        {
            Timestamp = timestamp,
        };
        Assert.Equal(timestamp, blockHeader.Timestamp);

        ModelAssert.Throws(blockHeader with
        {
            Timestamp = default,
        }, nameof(BlockHeader.Timestamp));
    }

    [Fact]
    public void Proposer()
    {
        var random = Rand.GetRandom(output);
        var proposer = Rand.Address(random);
        var blockHeader = Rand.BlockHeader(random) with
        {
            Proposer = proposer,
        };
        Assert.Equal(proposer, blockHeader.Proposer);

        ModelAssert.Throws(blockHeader with
        {
            Proposer = default,
        }, nameof(BlockHeader.Proposer));
    }

    [Fact]
    public void PreviousHash()
    {
        var random = Rand.GetRandom(output);
        var previousHash = Rand.BlockHash(random);
        var blockHeader = Rand.BlockHeader(random) with
        {
            PreviousBlockHash = previousHash,
        };
        Assert.Equal(previousHash, blockHeader.PreviousBlockHash);
    }

    [Fact]
    public void PreviousCommit()
    {
        var random = Rand.GetRandom(output);
        var previousCommit = Rand.BlockCommit(random);
        var blockHeader = Rand.BlockHeader(random) with
        {
            PreviousBlockCommit = previousCommit,
        };
        Assert.Equal(previousCommit, blockHeader.PreviousBlockCommit);
    }

    [Fact]
    public void PreviousStateRootHash()
    {
        var random = Rand.GetRandom(output);
        var previousStateRootHash = Rand.HashDigest<SHA256>(random);
        var blockHeader = Rand.BlockHeader(random) with
        {
            PreviousStateRootHash = previousStateRootHash,
        };
        Assert.Equal(previousStateRootHash, blockHeader.PreviousStateRootHash);
    }
}
