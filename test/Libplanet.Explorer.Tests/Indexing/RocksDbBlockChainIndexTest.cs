using System.Threading.Tasks;
using Xunit;

namespace Libplanet.Explorer.Tests.Indexing;

public class RocksDbBlockChainIndexTest: BlockChainIndexTest
{
    public RocksDbBlockChainIndexTest()
    {
        Fx = new RocksDbBlockChainIndexFixture(
            ChainFx.Chain.Store);
    }

    protected sealed override IBlockChainIndexFixture Fx {
        get;
        set;
    }

    [Theory]
    [MemberData(nameof(BooleanPermutation3))]
#pragma warning disable S2699 // Tests should include assertions
    public async Task GetBlockHashesMultiByteIndex(bool fromHalfway, bool throughHalfway, bool desc)
#pragma warning restore S2699 // Tests should include assertions
    {
        ChainFx = new GeneratedBlockChainFixture(
            RandomGenerator.Next(), byte.MaxValue + 2, 1, 1);
        Fx = new RocksDbBlockChainIndexFixture(
            ChainFx.Chain.Store);
        await GetBlockHashes(fromHalfway, throughHalfway, desc);
    }

    [Theory]
    [MemberData(nameof(BooleanPermutation3))]
#pragma warning disable S2699 // Tests should include assertions
    public async Task GetBlockHashesByMinerMultiByteIndex(
#pragma warning restore S2699 // Tests should include assertions
        bool fromHalfway, bool throughHalfway, bool desc)
    {
        ChainFx = new GeneratedBlockChainFixture(
            RandomGenerator.Next(), byte.MaxValue + 2, 1, 1);
        Fx = new RocksDbBlockChainIndexFixture(
            ChainFx.Chain.Store);
        await GetBlockHashesByMiner(fromHalfway, throughHalfway, desc);
    }

    [Fact]
    public async Task TipMultiByteIndex()
    {
        ChainFx = new GeneratedBlockChainFixture(
            RandomGenerator.Next(), byte.MaxValue + 2, 1, 1);
        Fx = new RocksDbBlockChainIndexFixture(
            ChainFx.Chain.Store);
        var tip = await Fx.Index.GetTipAsync();
        Assert.Equal(tip, Fx.Index.Tip);
        Assert.Equal(ChainFx.Chain.Tip.Hash, tip.Hash);
        Assert.Equal(ChainFx.Chain.Tip.Index, tip.Index);
    }

    [Fact]
#pragma warning disable S2699 // Tests should include assertions
    public async Task GetLastNonceByAddressMultiByteIndex()
#pragma warning restore S2699 // Tests should include assertions
    {
        ChainFx = new GeneratedBlockChainFixture(
            RandomGenerator.Next(), 2, byte.MaxValue + 2, 1);
        Fx = new RocksDbBlockChainIndexFixture(
            ChainFx.Chain.Store);
        await GetLastNonceByAddress();
    }

    [Theory]
    [MemberData(nameof(BooleanPermutation3))]
#pragma warning disable S2699 // Tests should include assertions
    public async Task GetSignedTxIdsByAddressMultiByteIndex(
#pragma warning restore S2699 // Tests should include assertions
        bool fromHalfway, bool throughHalfway, bool desc)
    {
        ChainFx = new GeneratedBlockChainFixture(
            RandomGenerator.Next(), 2, byte.MaxValue + 2, 1);
        Fx = new RocksDbBlockChainIndexFixture(
            ChainFx.Chain.Store);
        await GetSignedTxIdsByAddress(fromHalfway, throughHalfway, desc);
    }
}
