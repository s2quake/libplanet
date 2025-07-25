using Libplanet.Tests.Store;

namespace Libplanet.Net.Tests;

public class BlockCandidateTableTest
{
    [Fact]
    public void Add()
    {
        using var fx = new MemoryRepositoryFixture();
        var blockBranches = new BlockBranchCollection();
        var genesis = fx.GenesisBlock;

        // Ignore existing key
        var firstBranch = BlockBranch.Create(
                (fx.Block2, TestUtils.CreateBlockCommit(fx.Block2)),
                (fx.Block3, TestUtils.CreateBlockCommit(fx.Block3)),
                (fx.Block4, TestUtils.CreateBlockCommit(fx.Block4)));
        var secondBranch = BlockBranch.Create(
                (fx.Block3, TestUtils.CreateBlockCommit(fx.Block3)),
                (fx.Block4, TestUtils.CreateBlockCommit(fx.Block4)));
        blockBranches.Add(genesis.Header, firstBranch);
        Assert.Equal(1, blockBranches.Count);
        Assert.Throws<ArgumentException>(() => blockBranches.Add(genesis.Header, secondBranch));
        Assert.Equal(1, blockBranches.Count);
        var actualBranch = blockBranches[genesis.Header];
        Assert.Equal(actualBranch, firstBranch);
    }
}
