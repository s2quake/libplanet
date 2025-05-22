using Libplanet.Blockchain;
using Libplanet.Store;
using Libplanet.Tests.Store;
using Libplanet.Types.Crypto;
using Libplanet.Types.Tx;

namespace Libplanet.Tests.Blockchain.Policies;

public abstract class StagePolicyTest
{
    protected readonly BlockChainOptions _policy;
    protected readonly MemoryStoreFixture _fx;
    protected readonly BlockChain _blockChain;
    protected readonly PrivateKey _key;
    protected readonly Transaction[] _txs;

    protected StagePolicyTest()
    {
        _policy = new BlockChainOptions();
        _fx = new MemoryStoreFixture();
        var repository = new Repository();
        _blockChain = new BlockChain(_fx.GenesisBlock, repository, _policy);
        _key = new PrivateKey();
        _txs = Enumerable.Range(0, 5).Select(i =>
            new TransactionMetadata
            {
                Nonce = i,
                Signer = _key.Address,
                GenesisHash = _fx.GenesisBlock.BlockHash,
                Actions = [],
            }.Sign(_key))
        .ToArray();
    }

    protected StagedTransactionCollection StageTransactions => _blockChain.StagedTransactions;

    [Fact]
    public void Stage()
    {
        void AssertTxSetEqual(
            IEnumerable<Transaction> setOne,
            IEnumerable<Transaction> setTwo)
        {
            Assert.Equal(setOne.OrderBy(tx => tx.Id), setTwo.OrderBy(tx => tx.Id));
        }

        var duplicateNonceTx = new TransactionMetadata
        {
            Nonce = 2,
            Signer = _key.Address,
            GenesisHash = _fx.GenesisBlock.BlockHash,
            Actions = [],
        }.Sign(_key);

        Assert.Empty(StageTransactions);

        Assert.True(StageTransactions.TryAdd(_txs[0]));
        AssertTxSetEqual(_txs.Take(1), StageTransactions.Values);

        Assert.True(StageTransactions.TryAdd(_txs[1]));
        AssertTxSetEqual(_txs.Take(2), StageTransactions.Values);

        // If already staged, no exception is thrown.
        Assert.False(StageTransactions.TryAdd(_txs[0]));
        AssertTxSetEqual(_txs.Take(2), StageTransactions.Values);

        // Duplicate nonces can be staged.
        Assert.True(StageTransactions.TryAdd(_txs[2]));
        AssertTxSetEqual(_txs.Take(3), StageTransactions.Values);
        Assert.True(StageTransactions.TryAdd(duplicateNonceTx));
        AssertTxSetEqual(_txs.Take(3).Append(duplicateNonceTx), StageTransactions.Values);

        // If a transaction had been unstaged, it can be staged again.
        Assert.True(StageTransactions.Remove(_txs[2].Id));
        AssertTxSetEqual(_txs.Take(2).Append(duplicateNonceTx), StageTransactions.Values);
        Assert.True(StageTransactions.TryAdd(_txs[2]));
        AssertTxSetEqual(
            _txs.Take(2).Append(duplicateNonceTx).Append(_txs[2]),
            StageTransactions.Values);
    }

    [Fact]
    public void Unstage()
    {
        void AssertTxSetEqual(
            IEnumerable<Transaction> setOne,
            IEnumerable<Transaction> setTwo)
        {
            Assert.Equal(setOne.OrderBy(tx => tx.Id), setTwo.OrderBy(tx => tx.Id));
        }

        foreach (Transaction tx in _txs)
        {
            StageTransactions.Add(tx);
        }

        AssertTxSetEqual(_txs, StageTransactions.Values);

        Assert.True(StageTransactions.Remove(_txs[0].Id));
        AssertTxSetEqual(_txs.Skip(1), StageTransactions.Values);

        // If already unstaged, no exception is thrown.
        Assert.False(StageTransactions.Remove(_txs[0].Id));
        AssertTxSetEqual(_txs.Skip(1), StageTransactions.Values);

        Assert.True(StageTransactions.Remove(_txs[^1].Id));
        AssertTxSetEqual(_txs.Skip(1).SkipLast(1), StageTransactions.Values);

        Assert.True(StageTransactions.Remove(_txs[2].Id));
        AssertTxSetEqual(new[] { _txs[1], _txs[3] }, StageTransactions.Values);
    }

    [Fact]
    public void Ignore()
    {
        // Ignore prevents staging.
        Assert.Contains(_txs[0].Id, StageTransactions.Keys);
        StageTransactions.Remove(_txs[0].Id);
        Assert.DoesNotContain(_txs[0].Id, StageTransactions.Keys);
        Assert.False(StageTransactions.TryAdd(_txs[0]));
        Assert.Throws<KeyNotFoundException>(() => StageTransactions[_txs[0].Id]);

        // Ignore unstages.
        Assert.Contains(_txs[1].Id, StageTransactions.Keys);
        Assert.True(StageTransactions.TryAdd(_txs[1]));
        Assert.Equal(_txs[1], StageTransactions[_txs[1].Id]);
        StageTransactions.Remove(_txs[1].Id);
        Assert.DoesNotContain(_txs[1].Id, StageTransactions.Keys);
        Assert.Throws<KeyNotFoundException>(() => StageTransactions[_txs[1].Id]);
    }

    [Fact]
    public void Ignores()
    {
        // By default, nothing is ignored.
        foreach (Transaction tx in _txs)
        {
            Assert.DoesNotContain(tx.Id, StageTransactions.Keys);
        }

        // Staging has no effect on ignores.
        Assert.True(StageTransactions.TryAdd(_txs[0]));
        Assert.Contains(_txs[0].Id, StageTransactions.Keys);

        // Unstaging has no effect on ignores.
        Assert.True(StageTransactions.Remove(_txs[0].Id));
        Assert.DoesNotContain(_txs[0].Id, StageTransactions.Keys);

        // Only Ignore() ignores regardless of staged state.
        StageTransactions.Add(_txs[1]);
        StageTransactions.Remove(_txs[1].Id);
        StageTransactions.Remove(_txs[2].Id);
        Assert.DoesNotContain(_txs[1].Id, StageTransactions.Keys);
        Assert.DoesNotContain(_txs[2].Id, StageTransactions.Keys);
    }

    [Fact]
    public void Get()
    {
        foreach (Transaction tx in _txs)
        {
            Assert.Throws<KeyNotFoundException>(() => StageTransactions[tx.Id]);
        }

        StageTransactions.Add(_txs[0]);
        Assert.Equal(_txs[0], StageTransactions[_txs[0].Id]);

        foreach (Transaction tx in _txs.Skip(1))
        {
            Assert.Null(StageTransactions[tx.Id]);
        }

        StageTransactions.Remove(_txs[0].Id);
        Assert.Null(StageTransactions[_txs[0].Id]);

        foreach (Transaction tx in _txs.Skip(1))
        {
            Assert.Null(StageTransactions[tx.Id]);
        }
    }
}
