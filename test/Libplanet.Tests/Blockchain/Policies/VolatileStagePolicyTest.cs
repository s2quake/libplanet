using System.Threading;
using Libplanet.Types.Tx;

namespace Libplanet.Tests.Blockchain.Policies;

public class VolatileStagePolicyTest : StagePolicyTest
{
    [Fact]
    public void Lifetime()
    {
        TimeSpan timeBuffer = TimeSpan.FromSeconds(1);
        Transaction tx = new TransactionMetadata
        {
            Nonce = 0,
            Signer = _key.Address,
            GenesisHash = _fx.GenesisBlock.BlockHash,
            Actions = [],
            Timestamp = DateTimeOffset.UtcNow - StagePolicy.Lifetime + timeBuffer,
        }.Sign(_key);
        Assert.True(StagePolicy.Stage(tx));
        Assert.Equal(tx, StagePolicy[tx.Id]);
        Assert.Contains(tx, StagePolicy.Iterate());

        // On some targets TimeSpan * int does not exist.
        Thread.Sleep(timeBuffer);
        Thread.Sleep(timeBuffer);
        Assert.Null(StagePolicy[tx.Id]);
        Assert.DoesNotContain(tx, StagePolicy.Iterate());
    }

    // [Fact]
    // public void MaxLifetime()
    // {
    //     var stagePolicy = new VolatileStagePolicy(TimeSpan.MaxValue);
    //     Transaction tx = new TransactionMetadata
// {
    //         0,
    //         _key,
    //         _fx.GenesisBlock.BlockHash,
    //         []);
    //     Assert.True(stagePolicy.Stage(_chain, tx));
    // }

    [Fact]
    public void StageUnstage()
    {
        TimeSpan timeBuffer = TimeSpan.FromSeconds(1);
        Transaction validTx = new TransactionMetadata
        {
            Nonce = 0,
            Signer = _key.Address,
            GenesisHash = _fx.GenesisBlock.BlockHash,
            Actions = [],
            Timestamp = DateTimeOffset.UtcNow - StagePolicy.Lifetime + timeBuffer,
        }.Sign(_key);
        Transaction staleTx = new TransactionMetadata
        {
            Nonce = 0,
            Signer = _key.Address,
            GenesisHash = _fx.GenesisBlock.BlockHash,
            Actions = [],
            Timestamp = DateTimeOffset.UtcNow - StagePolicy.Lifetime - timeBuffer,
        }.Sign(_key);

        Assert.False(StagePolicy.Stage(staleTx));
        Assert.True(StagePolicy.Stage(validTx));
        Assert.False(StagePolicy.Stage(validTx));
        Assert.True(StagePolicy.Unstage(validTx.Id));
        Assert.False(StagePolicy.Unstage(validTx.Id));
    }
}
