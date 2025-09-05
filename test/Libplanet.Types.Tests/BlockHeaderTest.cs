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
        Assert.Equal("BlockHeader", attribute.TypeName);
    }

    [Fact]
    public void PropertyAttributes()
    {
        ModelTestUtility.AssertProperty<BlockHeader>(nameof(BlockHeader.Version), 0);
        ModelTestUtility.AssertProperty<BlockHeader>(nameof(BlockHeader.Height), 1);
        ModelTestUtility.AssertProperty<BlockHeader>(nameof(BlockHeader.Timestamp), 2);
        ModelTestUtility.AssertProperty<BlockHeader>(nameof(BlockHeader.Proposer), 3);
        ModelTestUtility.AssertProperty<BlockHeader>(nameof(BlockHeader.PreviousBlockHash), 4);
        ModelTestUtility.AssertProperty<BlockHeader>(nameof(BlockHeader.PreviousBlockCommit), 5);
        ModelTestUtility.AssertProperty<BlockHeader>(nameof(BlockHeader.PreviousStateRootHash), 6);
    }

    [Fact]
    public void SerializeAndDeserialize()
    {
        var random = RandomUtility.GetRandom(output);
        var expected = RandomUtility.BlockHeader(random);
        var serialized = ModelSerializer.SerializeToBytes(expected);
        var actual = ModelSerializer.DeserializeFromBytes(serialized);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Ctor()
    {
        var blockHeader = new BlockHeader();
        Assert.Equal(BlockHeader.CurrentVersion, blockHeader.Version);
        Assert.Equal(0L, blockHeader.Height);
        Assert.Equal(default, blockHeader.Timestamp);
        Assert.Equal(default, blockHeader.Proposer);
        Assert.Equal(default, blockHeader.PreviousBlockHash);
        Assert.Equal(default, blockHeader.PreviousBlockCommit);
        Assert.Equal(default, blockHeader.PreviousStateRootHash);
    }

    [Fact]
    public void Version()
    {
        var random = RandomUtility.GetRandom(output);
        var blockHeader = RandomUtility.BlockHeader(random) with
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
        var random = RandomUtility.GetRandom(output);
        var height = RandomUtility.NonNegative(random);
        var blockHeader = RandomUtility.BlockHeader(random) with
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
        var random = RandomUtility.GetRandom(output);
        var timestamp = RandomUtility.DateTimeOffset(random);
        var blockHeader = RandomUtility.BlockHeader(random) with
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
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Address(random);
        var blockHeader = RandomUtility.BlockHeader(random) with
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
        var random = RandomUtility.GetRandom(output);
        var previousHash = RandomUtility.BlockHash(random);
        var blockHeader = RandomUtility.BlockHeader(random) with
        {
            PreviousBlockHash = previousHash,
        };
        Assert.Equal(previousHash, blockHeader.PreviousBlockHash);
    }

    [Fact]
    public void PreviousCommit()
    {
        var random = RandomUtility.GetRandom(output);
        var previousCommit = RandomUtility.BlockCommit(random);
        var blockHeader = RandomUtility.BlockHeader(random) with
        {
            PreviousBlockCommit = previousCommit,
        };
        Assert.Equal(previousCommit, blockHeader.PreviousBlockCommit);
    }

    [Fact]
    public void PreviousStateRootHash()
    {
        var random = RandomUtility.GetRandom(output);
        var previousStateRootHash = RandomUtility.HashDigest<SHA256>(random);
        var blockHeader = RandomUtility.BlockHeader(random) with
        {
            PreviousStateRootHash = previousStateRootHash,
        };
        Assert.Equal(previousStateRootHash, blockHeader.PreviousStateRootHash);
    }
}
