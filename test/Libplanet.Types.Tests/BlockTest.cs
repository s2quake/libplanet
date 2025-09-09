using System.Reflection;
using Libplanet.Serialization;
using Libplanet.TestUtilities;

namespace Libplanet.Types.Tests;

public sealed class BlockTest(ITestOutputHelper output)
{
    [Fact]
    public void Attribute()
    {
        var attribute = typeof(Block).GetCustomAttribute<ModelAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal("blk", attribute.TypeName);
        Assert.Equal(1, attribute.Version);
    }

    [Fact]
    public void SerializeAndDeserialize()
    {
        var random = Rand.GetRandom(output);
        var block1 = Rand.Block(random);
        var serialized = ModelSerializer.SerializeToBytes(block1);
        var block2 = ModelSerializer.DeserializeFromBytes(serialized);
        Assert.Equal(block1, block2);
        Assert.Equal(block1.GetHashCode(), block2.GetHashCode());
    }

    [Fact]
    public void BaseTest()
    {
        var random = Rand.GetRandom(output);
        var block = Rand.Block(random);

        Assert.Equal(BlockHash.HashData(ModelSerializer.SerializeToBytes(block)), block.BlockHash);
        Assert.Equal(block.Header.Height, block.Height);
        Assert.Equal(block.Header.Version, block.Version);
        Assert.Equal(block.Header.Timestamp, block.Timestamp);
        Assert.Equal(block.Header.Proposer, block.Proposer);
        Assert.Equal(block.Header.PreviousBlockHash, block.PreviousBlockHash);
        Assert.Equal(block.Header.PreviousBlockCommit, block.PreviousBlockCommit);
        Assert.Equal(block.Header.PreviousStateRootHash, block.PreviousStateRootHash);
        Assert.Equal(block.Content.Transactions, block.Transactions);
        Assert.Equal(block.Content.Evidence, block.Evidences);
    }

    [Fact]
    public void ToStringTest()
    {
        var random = Rand.GetRandom(output);
        var block = Rand.Block(random);
        Assert.Equal(block.BlockHash.ToString(), block.ToString());
    }

    [Fact]
    public void Verify_Fail()
    {
        var random = Rand.GetRandom(output);
        var block = Rand.Block(random);
        Assert.False(block.Verify());
    }

    [Fact]
    public void Verify()
    {
        var random = Rand.GetRandom(output);
        var block = Rand.Block(random);
        Assert.True(block.Verify());
    }
}
