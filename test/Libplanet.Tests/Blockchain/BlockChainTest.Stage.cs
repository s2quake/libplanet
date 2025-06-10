using Libplanet.State.Tests.Actions;
using Libplanet.Types;

namespace Libplanet.Tests.Blockchain;

public partial class BlockChainTest
{
    [Fact]
    public void StageTransaction()
    {
        var txs = new HashSet<Transaction>()
        {
            _fx.Transaction1,
            _fx.Transaction2,
        };
        Assert.Empty(_blockChain.StagedTransactions);

        _blockChain.StagedTransactions.AddRange(txs);
        Assert.Equal(txs, _blockChain.StagedTransactions.Values.ToHashSet());
    }

    [Fact]
    public void StageTransactionWithDifferentGenesis()
    {
        var tx1Key = new PrivateKey();
        var tx1 = new TransactionMetadata
        {
            Nonce = 0,
            Signer = tx1Key.Address,
            GenesisHash = _blockChain.Genesis.BlockHash,
            Actions = [],
        }.Sign(tx1Key);
        var tx2Key = new PrivateKey();
        var tx2 = new TransactionMetadata
        {
            Nonce = 0,
            Signer = tx2Key.Address,
            Actions = [],
        }.Sign(tx2Key);
        var tx3Key = new PrivateKey();
        var tx3 = new TransactionMetadata
        {
            Nonce = 0,
            Signer = tx3Key.Address,
            Actions = [],
        }.Sign(tx3Key);

        Assert.True(_blockChain.StagedTransactions.TryAdd(tx1));
        Assert.Equal(1, _blockChain.StagedTransactions.Keys.Count());
        Assert.Throws<InvalidOperationException>(() => _blockChain.StagedTransactions.Add(tx2));
        Assert.Equal(1, _blockChain.StagedTransactions.Keys.Count());
        Assert.Throws<InvalidOperationException>(() => _blockChain.StagedTransactions.Add(tx3));
        Assert.Equal(1, _blockChain.StagedTransactions.Keys.Count());
    }

    [Fact]
    public void TransactionsWithDuplicatedNonce()
    {
        var key = new PrivateKey();

        Transaction tx_0_0 = _fx.MakeTransaction(
            Array.Empty<DumbAction>(),
            nonce: 0,
            privateKey: key);
        Transaction tx_0_1 = _fx.MakeTransaction(
            Array.Empty<DumbAction>(),
            nonce: 0,
            privateKey: key);
        Transaction tx_1_0 = _fx.MakeTransaction(
            Array.Empty<DumbAction>(),
            nonce: 1,
            privateKey: key);
        Transaction tx_1_1 = _fx.MakeTransaction(
            Array.Empty<DumbAction>(),
            nonce: 1,
            privateKey: key);

        // stage tx_0_0 -> mine tx_0_0 -> stage tx_0_1
        Assert.True(_blockChain.StagedTransactions.TryAdd(tx_0_0));
        var block = _blockChain.ProposeBlock(key);
        _blockChain.Append(block, TestUtils.CreateBlockCommit(block));
        Assert.Empty(_blockChain.StagedTransactions.Keys);
        // Assert.Empty(_blockChain.StagedTransactions.Iterate(filtered: true));
        Assert.Empty(_blockChain.StagedTransactions.Values);
        // should still able to stage a low nonce tx
        Assert.True(_blockChain.StagedTransactions.TryAdd(tx_0_1));
        // tx_0_1 is still staged, just filtered.
        Assert.Empty(_blockChain.StagedTransactions.Keys);
        // Assert.Empty(_blockChain.StagedTransactions.Iterate(filtered: true));
        Assert.NotEmpty(_blockChain.StagedTransactions.Values);

        // stage tx_1_0 -> stage tx_1_1 -> mine tx_1_0 or tx_1_1
        Assert.True(_blockChain.StagedTransactions.TryAdd(tx_1_0));
        Assert.True(_blockChain.StagedTransactions.TryAdd(tx_1_1));
        var txIds = new List<TxId>() { tx_1_0.Id, tx_1_1.Id };
        Assert.Equal(2, _blockChain.StagedTransactions.Keys.Count());
        Assert.Equal(
            txIds.OrderBy(id => id),
            _blockChain.StagedTransactions.Keys.OrderBy(id => id));
        block = _blockChain.ProposeBlock(key);
        _blockChain.Append(block, TestUtils.CreateBlockCommit(block));
        // tx_0_1 and tx_1_x should be still staged, just filtered
        Assert.Empty(_blockChain.StagedTransactions.Keys);
        // Assert.Empty(_blockChain.StagedTransactions.Iterate(filtered: true));
        Assert.Equal(2, _blockChain.StagedTransactions.Count);
    }

    [Fact]
    public void UnstageTransaction()
    {
        Transaction[] txs = { _fx.Transaction1, _fx.Transaction2 };
        Assert.Empty(_blockChain.StagedTransactions.Keys);

        _blockChain.StagedTransactions.AddRange(txs);

        HashSet<TxId> txIds = txs.Select(tx => tx.Id).ToHashSet();
        HashSet<TxId> stagedTxIds = _blockChain.StagedTransactions.Keys.ToHashSet();

        Assert.Equal(txIds, stagedTxIds);

        Assert.True(_blockChain.StagedTransactions.Remove(_fx.Transaction1.Id));
        Assert.True(_blockChain.StagedTransactions.Remove(_fx.Transaction2.Id));

        Assert.Empty(_blockChain.StagedTransactions.Keys);
    }
}
