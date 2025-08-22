using Libplanet.State.Tests.Actions;
using Libplanet.TestUtilities;
using Libplanet.TestUtilities.Extensions;
using Libplanet.Types;

namespace Libplanet.Tests.Blockchain;

public partial class BlockchainTest
{
    [Fact]
    public void StageTransaction()
    {
        var txs = new HashSet<Transaction>()
        {
            _fx.Transaction1,
            _fx.Transaction2,
        };
        Assert.Empty(_blockchain.StagedTransactions);

        _blockchain.StagedTransactions.AddRange(txs);
        Assert.Equal(txs, _blockchain.StagedTransactions.Values.ToHashSet());
    }

    [Fact]
    public void StageTransactionWithDifferentGenesis()
    {
        var random = RandomUtility.GetRandom(_output);
        var tx1Signer = RandomUtility.Signer(random);
        var tx1 = new TransactionMetadata
        {
            Nonce = 0,
            Signer = tx1Signer.Address,
            GenesisBlockHash = _blockchain.Genesis.BlockHash,
            Actions = [],
        }.Sign(tx1Signer);
        var tx2Signer = RandomUtility.Signer(random);
        var tx2 = new TransactionMetadata
        {
            Nonce = 0,
            Signer = tx2Signer.Address,
            Actions = [],
        }.Sign(tx2Signer);
        var tx3Signer = RandomUtility.Signer(random);
        var tx3 = new TransactionMetadata
        {
            Nonce = 0,
            Signer = tx3Signer.Address,
            Actions = [],
        }.Sign(tx3Signer);

        _blockchain.StagedTransactions.Add(tx1);
        Assert.Single(_blockchain.StagedTransactions.Keys);
        Assert.Throws<InvalidOperationException>(() => _blockchain.StagedTransactions.Add(tx2));
        Assert.Single(_blockchain.StagedTransactions.Keys);
        Assert.Throws<InvalidOperationException>(() => _blockchain.StagedTransactions.Add(tx3));
        Assert.Single(_blockchain.StagedTransactions.Keys);
    }

    [Fact]
    public void TransactionsWithDuplicatedNonce()
    {
        var random = RandomUtility.GetRandom(_output);
        var signer = RandomUtility.Signer(random);

        Transaction tx_0_0 = _fx.MakeTransaction(
            Array.Empty<DumbAction>(),
            nonce: 0,
            signer: signer);
        Transaction tx_0_1 = _fx.MakeTransaction(
            Array.Empty<DumbAction>(),
            nonce: 0,
            signer: signer);
        Transaction tx_1_0 = _fx.MakeTransaction(
            Array.Empty<DumbAction>(),
            nonce: 1,
            signer: signer);
        Transaction tx_1_1 = _fx.MakeTransaction(
            Array.Empty<DumbAction>(),
            nonce: 1,
            signer: signer);

        // stage tx_0_0 -> mine tx_0_0 -> stage tx_0_1
        _blockchain.StagedTransactions.Add(tx_0_0);
        var block = _blockchain.ProposeBlock(signer);
        _blockchain.Append(block, TestUtils.CreateBlockCommit(block));
        Assert.Empty(_blockchain.StagedTransactions.Keys);
        // Assert.Empty(_blockChain.StagedTransactions.Iterate(filtered: true));
        Assert.Empty(_blockchain.StagedTransactions.Values);
        // should still able to stage a low nonce tx
        _blockchain.StagedTransactions.Add(tx_0_1);
        // tx_0_1 is still staged, just filtered.
        Assert.Empty(_blockchain.StagedTransactions.Keys);
        // Assert.Empty(_blockChain.StagedTransactions.Iterate(filtered: true));
        Assert.NotEmpty(_blockchain.StagedTransactions.Values);

        // stage tx_1_0 -> stage tx_1_1 -> mine tx_1_0 or tx_1_1
        _blockchain.StagedTransactions.Add(tx_1_0);
        _blockchain.StagedTransactions.Add(tx_1_1);
        var txIds = new List<TxId>() { tx_1_0.Id, tx_1_1.Id };
        Assert.Equal(2, _blockchain.StagedTransactions.Keys.Count());
        Assert.Equal(
            txIds.OrderBy(id => id),
            _blockchain.StagedTransactions.Keys.OrderBy(id => id));
        block = _blockchain.ProposeBlock(signer);
        _blockchain.Append(block, TestUtils.CreateBlockCommit(block));
        // tx_0_1 and tx_1_x should be still staged, just filtered
        Assert.Empty(_blockchain.StagedTransactions.Keys);
        // Assert.Empty(_blockChain.StagedTransactions.Iterate(filtered: true));
        Assert.Equal(2, _blockchain.StagedTransactions.Count);
    }

    [Fact]
    public void UnstageTransaction()
    {
        Transaction[] txs = { _fx.Transaction1, _fx.Transaction2 };
        Assert.Empty(_blockchain.StagedTransactions.Keys);

        _blockchain.StagedTransactions.AddRange(txs);

        HashSet<TxId> txIds = txs.Select(tx => tx.Id).ToHashSet();
        HashSet<TxId> stagedTxIds = _blockchain.StagedTransactions.Keys.ToHashSet();

        Assert.Equal(txIds, stagedTxIds);

        Assert.True(_blockchain.StagedTransactions.Remove(_fx.Transaction1.Id));
        Assert.True(_blockchain.StagedTransactions.Remove(_fx.Transaction2.Id));

        Assert.Empty(_blockchain.StagedTransactions.Keys);
    }
}
