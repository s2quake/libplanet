using Libplanet.TestUtilities;
using Libplanet.Types;

namespace Libplanet.Tests;

public partial class BlockchainTest
{
    [Fact]
    public void StageTransaction()
    {
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        ImmutableSortedSet<Transaction> txs =
        [
            blockchain.CreateTransaction(proposer),
            blockchain.CreateTransaction(proposer),
        ];
        Assert.Empty(blockchain.StagedTransactions);

        blockchain.StagedTransactions.AddRange(txs);
        Assert.Equal(txs, [.. blockchain.StagedTransactions.Values]);
    }

    [Fact]
    public void StageTransactionWithDifferentGenesis()
    {
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);

        var tx1Signer = RandomUtility.Signer(random);
        var tx1 = new TransactionBuilder
        {
            Nonce = 0,
            GenesisBlockHash = blockchain.Genesis.BlockHash,
            Actions = [],
        }.Create(tx1Signer);
        var tx2Signer = RandomUtility.Signer(random);
        var tx2 = new TransactionBuilder
        {
            Nonce = 0,
            Actions = [],
        }.Create(tx2Signer);
        var tx3Signer = RandomUtility.Signer(random);
        var tx3 = new TransactionBuilder
        {
            Nonce = 0,
            Actions = [],
        }.Create(tx3Signer);

        blockchain.StagedTransactions.Add(tx1);
        Assert.Single(blockchain.StagedTransactions.Keys);
        Assert.Throws<ArgumentException>("transaction", () => blockchain.StagedTransactions.Add(tx2));
        Assert.Single(blockchain.StagedTransactions.Keys);
        Assert.Throws<ArgumentException>("transaction", () => blockchain.StagedTransactions.Add(tx3));
        Assert.Single(blockchain.StagedTransactions.Keys);
    }

    [Fact]
    public void TransactionsWithDuplicatedNonce()
    {
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        var signer = RandomUtility.Signer(random);

        var tx_0_0 = blockchain.CreateTransaction(signer, new() { Nonce = 0L });
        var tx_0_1 = blockchain.CreateTransaction(signer, new() { Nonce = 0L });
        var tx_1_0 = blockchain.CreateTransaction(signer, new() { Nonce = 1L });
        var tx_1_1 = blockchain.CreateTransaction(signer, new() { Nonce = 1L });

        // stage tx_0_0 -> mine tx_0_0 -> stage tx_0_1
        blockchain.StagedTransactions.Add(tx_0_0);
        blockchain.ProposeAndAppend(signer);
        Assert.Empty(blockchain.StagedTransactions.Keys);
        Assert.Empty(blockchain.StagedTransactions.Values);
        // should still able to stage a low nonce tx
        blockchain.StagedTransactions.Add(tx_0_1);
        // tx_0_1 is still staged, just filtered.
        Assert.Single(blockchain.StagedTransactions.Keys);
        Assert.Single(blockchain.StagedTransactions.Values);
        blockchain.StagedTransactions.Prune();
        Assert.Empty(blockchain.StagedTransactions.Keys);
        Assert.Empty(blockchain.StagedTransactions.Values);

        // stage tx_1_0 -> stage tx_1_1 -> mine tx_1_0 or tx_1_1
        blockchain.StagedTransactions.Add(tx_1_0);
        blockchain.StagedTransactions.Add(tx_1_1);
        ImmutableSortedSet<TxId> txIds = [tx_1_0.Id, tx_1_1.Id];
        Assert.Equal(2, blockchain.StagedTransactions.Keys.Count());
        Assert.Equal(
            txIds,
            [.. blockchain.StagedTransactions.Keys]);
        blockchain.ProposeAndAppend(signer);
        // tx_0_1 and tx_1_x should be still staged, just filtered
        Assert.Single(blockchain.StagedTransactions.Keys);
        Assert.Single(blockchain.StagedTransactions.Values);
        blockchain.StagedTransactions.Prune();
        Assert.Empty(blockchain.StagedTransactions.Keys);
        Assert.Empty(blockchain.StagedTransactions.Values);
    }

    [Fact]
    public void UnstageTransaction()
    {
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        Transaction[] txs =
        [
            blockchain.CreateTransaction(proposer),
            blockchain.CreateTransaction(proposer)
        ];
        Assert.Empty(blockchain.StagedTransactions.Keys);

        blockchain.StagedTransactions.AddRange(txs);

        var txIds = txs.Select(tx => tx.Id).ToImmutableSortedSet();
        var stagedTxIds = blockchain.StagedTransactions.Keys.ToImmutableSortedSet();

        Assert.Equal(txIds, stagedTxIds);

        Assert.True(blockchain.StagedTransactions.Remove(txs[0].Id));
        Assert.True(blockchain.StagedTransactions.Remove(txs[1].Id));

        Assert.Empty(blockchain.StagedTransactions.Keys);
    }
}
