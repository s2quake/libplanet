using Libplanet.Action;
using Libplanet.Action.Tests.Common;
using Libplanet.Blockchain;
using Libplanet.Tests.Store;
using Libplanet.Types.Crypto;
using Libplanet.Types.Tx;

namespace Libplanet.Tests.Blockchain.Policies;

public abstract class StagePolicyTest
{
    protected readonly BlockPolicy _policy;
    protected readonly MemoryStoreFixture _fx;
    protected readonly BlockChain _chain;
    protected readonly PrivateKey _key;
    protected readonly Transaction[] _txs;

    protected StagePolicyTest()
    {
        _policy = new BlockPolicy();
        _fx = new MemoryStoreFixture();
        _chain = BlockChain.Create(
            _policy,
            _fx.Store,
            _fx.StateStore,
            _fx.GenesisBlock);
        _key = new PrivateKey();
        _txs = Enumerable.Range(0, 5).Select(i =>
            Transaction.Create(
                i,
                _key,
                _fx.GenesisBlock.BlockHash,
                []))
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

        var duplicateNonceTx = Transaction.Create(
            2,
            _key,
            _fx.GenesisBlock.BlockHash,
            []);

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
        Assert.False(StagePolicy.Ignores(_txs[0].Id));
        StagePolicy.Ignore(_txs[0].Id);
        Assert.True(StagePolicy.Ignores(_txs[0].Id));
        Assert.False(StagePolicy.Stage(_txs[0]));
        Assert.Null(StagePolicy.Get(_txs[0].Id));

        // Ignore unstages.
        Assert.False(StagePolicy.Ignores(_txs[1].Id));
        Assert.True(StagePolicy.Stage(_txs[1]));
        Assert.Equal(_txs[1], StagePolicy.Get(_txs[1].Id));
        StagePolicy.Ignore(_txs[1].Id);
        Assert.True(StagePolicy.Ignores(_txs[1].Id));
        Assert.Null(StagePolicy.Get(_txs[1].Id));
    }

    [Fact]
    public void Ignores()
    {
        // By default, nothing is ignored.
        foreach (Transaction tx in _txs)
        {
            Assert.False(StagePolicy.Ignores(tx.Id));
        }

        // Staging has no effect on ignores.
        Assert.True(StagePolicy.Stage(_txs[0]));
        Assert.False(StagePolicy.Ignores(_txs[0].Id));

        // Unstaging has no effect on ignores.
        Assert.True(StagePolicy.Unstage(_txs[0].Id));
        Assert.False(StagePolicy.Ignores(_txs[0].Id));

        // Only Ignore() ignores regardless of staged state.
        StagePolicy.Stage(_txs[1]);
        StagePolicy.Ignore(_txs[1].Id);
        StagePolicy.Ignore(_txs[2].Id);
        Assert.True(StagePolicy.Ignores(_txs[1].Id));
        Assert.True(StagePolicy.Ignores(_txs[2].Id));
    }

    [Fact]
    public void Get()
    {
        foreach (Transaction tx in _txs)
        {
            Assert.Null(StagePolicy.Get(tx.Id));
        }

        StagePolicy.Stage(_txs[0]);
        Assert.Equal(_txs[0], StagePolicy.Get(_txs[0].Id));

        foreach (Transaction tx in _txs.Skip(1))
        {
            Assert.Null(StagePolicy.Get(tx.Id));
        }

        StagePolicy.Unstage(_txs[0].Id);
        Assert.Null(StagePolicy.Get(_txs[0].Id));

        foreach (Transaction tx in _txs.Skip(1))
        {
            Assert.Null(StagePolicy.Get(tx.Id));
        }
    }
}
