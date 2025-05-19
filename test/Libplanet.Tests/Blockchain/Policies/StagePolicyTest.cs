using Libplanet.Blockchain;
using Libplanet.Tests.Store;
using Libplanet.Types.Crypto;
using Libplanet.Types.Tx;

namespace Libplanet.Tests.Blockchain.Policies;

public abstract class StagePolicyTest
{
    protected readonly BlockChainOptions _policy;
    protected readonly MemoryStoreFixture _fx;
    protected readonly BlockChain _chain;
    protected readonly PrivateKey _key;
    protected readonly Transaction[] _txs;

    protected StagePolicyTest()
    {
        _policy = new BlockChainOptions();
        _fx = new MemoryStoreFixture();
        _chain = BlockChain.Create(_fx.GenesisBlock, _policy);
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

    protected StagedTransactionCollection StagePolicy => _chain.StagedTransactions;

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

        Assert.Empty(StagePolicy.Iterate());

        Assert.True(StagePolicy.Stage(_txs[0]));
        AssertTxSetEqual(_txs.Take(1), StagePolicy.Iterate());

        Assert.True(StagePolicy.Stage(_txs[1]));
        AssertTxSetEqual(_txs.Take(2), StagePolicy.Iterate());

        // If already staged, no exception is thrown.
        Assert.False(StagePolicy.Stage(_txs[0]));
        AssertTxSetEqual(_txs.Take(2), StagePolicy.Iterate());

        // Duplicate nonces can be staged.
        Assert.True(StagePolicy.Stage(_txs[2]));
        AssertTxSetEqual(_txs.Take(3), StagePolicy.Iterate());
        Assert.True(StagePolicy.Stage(duplicateNonceTx));
        AssertTxSetEqual(_txs.Take(3).Append(duplicateNonceTx), StagePolicy.Iterate());

        // If a transaction had been unstaged, it can be staged again.
        Assert.True(StagePolicy.Unstage(_txs[2].Id));
        AssertTxSetEqual(_txs.Take(2).Append(duplicateNonceTx), StagePolicy.Iterate());
        Assert.True(StagePolicy.Stage(_txs[2]));
        AssertTxSetEqual(
            _txs.Take(2).Append(duplicateNonceTx).Append(_txs[2]),
            StagePolicy.Iterate());
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
            StagePolicy.Stage(tx);
        }

        AssertTxSetEqual(_txs, StagePolicy.Iterate());

        Assert.True(StagePolicy.Unstage(_txs[0].Id));
        AssertTxSetEqual(_txs.Skip(1), StagePolicy.Iterate());

        // If already unstaged, no exception is thrown.
        Assert.False(StagePolicy.Unstage(_txs[0].Id));
        AssertTxSetEqual(_txs.Skip(1), StagePolicy.Iterate());

        Assert.True(StagePolicy.Unstage(_txs[^1].Id));
        AssertTxSetEqual(_txs.Skip(1).SkipLast(1), StagePolicy.Iterate());

        Assert.True(StagePolicy.Unstage(_txs[2].Id));
        AssertTxSetEqual(new[] { _txs[1], _txs[3] }, StagePolicy.Iterate());
    }

    [Fact]
    public void Ignore()
    {
        // Ignore prevents staging.
        Assert.Contains(_txs[0].Id, StagePolicy.Keys);
        StagePolicy.Ignore(_txs[0].Id);
        Assert.DoesNotContain(_txs[0].Id, StagePolicy.Keys);
        Assert.False(StagePolicy.Stage(_txs[0]));
        Assert.Throws<KeyNotFoundException>(() => StagePolicy[_txs[0].Id]);

        // Ignore unstages.
        Assert.Contains(_txs[1].Id, StagePolicy.Keys);
        Assert.True(StagePolicy.Stage(_txs[1]));
        Assert.Equal(_txs[1], StagePolicy[_txs[1].Id]);
        StagePolicy.Ignore(_txs[1].Id);
        Assert.DoesNotContain(_txs[1].Id, StagePolicy.Keys);
        Assert.Throws<KeyNotFoundException>(() => StagePolicy[_txs[1].Id]);
    }

    [Fact]
    public void Ignores()
    {
        // By default, nothing is ignored.
        foreach (Transaction tx in _txs)
        {
            Assert.DoesNotContain(tx.Id, StagePolicy.Keys);
        }

        // Staging has no effect on ignores.
        Assert.True(StagePolicy.Stage(_txs[0]));
        Assert.Contains(_txs[0].Id, StagePolicy.Keys);

        // Unstaging has no effect on ignores.
        Assert.True(StagePolicy.Unstage(_txs[0].Id));
        Assert.DoesNotContain(_txs[0].Id, StagePolicy.Keys);

        // Only Ignore() ignores regardless of staged state.
        StagePolicy.Stage(_txs[1]);
        StagePolicy.Ignore(_txs[1].Id);
        StagePolicy.Ignore(_txs[2].Id);
        Assert.DoesNotContain(_txs[1].Id, StagePolicy.Keys);
        Assert.DoesNotContain(_txs[2].Id, StagePolicy.Keys);
    }

    [Fact]
    public void Get()
    {
        foreach (Transaction tx in _txs)
        {
            Assert.Throws<KeyNotFoundException>(() => StagePolicy[tx.Id]);
        }

        StagePolicy.Stage(_txs[0]);
        Assert.Equal(_txs[0], StagePolicy[_txs[0].Id]);

        foreach (Transaction tx in _txs.Skip(1))
        {
            Assert.Null(StagePolicy[tx.Id]);
        }

        StagePolicy.Unstage(_txs[0].Id);
        Assert.Null(StagePolicy[_txs[0].Id]);

        foreach (Transaction tx in _txs.Skip(1))
        {
            Assert.Null(StagePolicy[tx.Id]);
        }
    }
}
