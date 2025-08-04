using System.Threading.Tasks;
using Libplanet.TestUtilities.Extensions;
using Libplanet.Types;

namespace Libplanet.Tests.Blockchain.Policies;

public class VolatileStagePolicyTest : StagePolicyTest
{
    [Fact]
    public async Task Lifetime()
    {
        var timeBuffer = TimeSpan.FromSeconds(1);
        var tx = new TransactionMetadata
        {
            Nonce = 0,
            Signer = _key.Address,
            GenesisHash = _fx.GenesisBlock.BlockHash,
            Actions = [],
            Timestamp = DateTimeOffset.UtcNow - StageTransactions.Lifetime + timeBuffer,
        }.Sign(_key);
        StageTransactions.Add(tx);
        Assert.Equal(tx, StageTransactions[tx.Id]);
        Assert.Contains(tx, StageTransactions.Values);

        // On some targets TimeSpan * int does not exist.
        await Task.Delay(timeBuffer);
        await Task.Delay(timeBuffer);
        Assert.Null(StageTransactions[tx.Id]);
        Assert.DoesNotContain(tx, StageTransactions.Values);
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
            Timestamp = DateTimeOffset.UtcNow - StageTransactions.Lifetime + timeBuffer,
        }.Sign(_key);
        Transaction staleTx = new TransactionMetadata
        {
            Nonce = 0,
            Signer = _key.Address,
            GenesisHash = _fx.GenesisBlock.BlockHash,
            Actions = [],
            Timestamp = DateTimeOffset.UtcNow - StageTransactions.Lifetime - timeBuffer,
        }.Sign(_key);

        Assert.Throws<ArgumentException>(() => StageTransactions.Add(staleTx));
        StageTransactions.Add(validTx);
        Assert.Throws<ArgumentException>(() => StageTransactions.Add(validTx));
        Assert.True(StageTransactions.Remove(validTx.Id));
        Assert.False(StageTransactions.Remove(validTx.Id));
    }
}
