using BenchmarkDotNet.Attributes;
using Libplanet.Tests;
using Libplanet.Tests.Store;
using Libplanet.Types;

namespace Libplanet.Benchmarks;

public class Store
{
    private readonly ImmutableArray<Block> _blocks;
    private readonly int BlocksCount = default;
    private readonly ImmutableArray<Transaction> Txs = default;
    private RepositoryFixture? _fx = null;
    private readonly int TxsCount = default;
    private Libplanet.Data.Repository _store;
    // private Chain _chain;

    public Store()
    {
        var blocks = new List<Block>();
        var txs = new List<Transaction>();
        Block genesis = TestUtils.ProposeGenesisBlock(TestUtils.GenesisProposer);
        blocks.Add(genesis);
        Block block = genesis;
        var signer = new PrivateKey().AsSigner();
        long nonce = 0;
        for (int i = 0; i < 500; i++)
        {
            var blockTxs = new List<Transaction>();
            for (int j = 0; j < i % 5; j++)
            {
                blockTxs.Add(new TransactionMetadata
                {
                    Nonce = nonce++,
                    Signer = signer.Address,
                    GenesisBlockHash = genesis.BlockHash,
                    Actions = [],
                }.Sign(signer));
            }
            block = TestUtils.ProposeNextBlock(
                block, TestUtils.GenesisProposer, [.. blockTxs]);
            blocks.Add(block);
            txs.AddRange(blockTxs);
        }

        _blocks = blocks.ToImmutableArray();
        BlocksCount = _blocks.Length;
        Txs = txs.ToImmutableArray();
        TxsCount = Txs.Length;
    }

    [IterationSetup]
    public void InitializeFixture()
    {
        _fx = new MemoryRepositoryFixture();
        _store = _fx.Repository;
    }

    [IterationCleanup]
    public void FinalizeFixture()
    {
        _fx.Dispose();
    }

    [Benchmark]
    public void PutFirstEmptyBlock()
    {
        _store.Append(_blocks[0], default);
    }

    [Benchmark]
    public void PutFirstBlockWithTxs()
    {
        _store.Append(_blocks[5], default);
    }

    [IterationSetup(
        Targets = new[]
        {
            nameof(PutBlockOnManyBlocks),
            nameof(GetOldBlockOutOfManyBlocks),
            nameof(GetRecentBlockOutOfManyBlocks),
        })]
    public void PutManyBlocks()
    {
        InitializeFixture();
        int i = 0;
        foreach (Block block in _blocks)
        {
            _store.Append(block, default);
            i++;
            if (i >= _blocks.Length - 1)
            {
                break;
            }
        }
    }

    [Benchmark]
    public void PutBlockOnManyBlocks()
    {
        _store.Append(_blocks[BlocksCount - 1], default);
    }

    [Benchmark]
    public Block GetOldBlockOutOfManyBlocks()
    {
        // Note that why this benchmark method returns something is
        // because without this JIT can remove the below statement at all
        // during dead code elimination optimization.
        // https://benchmarkdotnet.org/articles/guides/good-practices.html#avoid-dead-code-elimination
        return _store.GetBlock(_blocks[0].BlockHash);
    }

    [Benchmark]
    public Block GetRecentBlockOutOfManyBlocks()
    {
        // Note that why this benchmark method returns something is
        // because without this JIT can remove the below statement at all
        // during dead code elimination optimization.
        // https://benchmarkdotnet.org/articles/guides/good-practices.html#avoid-dead-code-elimination
        return _store.GetBlock(_blocks[BlocksCount - 2].BlockHash);
    }

    [Benchmark]
    public Block TryGetNonExistentBlockHash()
    {
        // Note that why this benchmark method returns something is
        // because without this JIT can remove the below statement at all
        // during dead code elimination optimization.
        // https://benchmarkdotnet.org/articles/guides/good-practices.html#avoid-dead-code-elimination
        return _store.GetBlock(blockHash: default);
    }

    [Benchmark]
    public void PutFirstTx()
    {
        _store.PendingTransactions.Add(Txs[0]);
    }

    [IterationSetup(
        Targets = new[]
        {
            nameof(PutTxOnManyTxs),
            nameof(GetOldTxOutOfManyTxs),
            nameof(GetRecentTxOutOfManyTxs),
        })]
    public void PutManyTxs()
    {
        InitializeFixture();
        int i = 0;
        foreach (Transaction tx in Txs)
        {
            _store.PendingTransactions.Add(tx);
            i++;
            if (i >= Txs.Length - 1)
            {
                break;
            }
        }
    }

    [Benchmark]
    public void PutTxOnManyTxs()
    {
        _store.PendingTransactions.Add(Txs[TxsCount - 1]);
    }

    [Benchmark]
    public Transaction GetOldTxOutOfManyTxs()
    {
        // Note that why this benchmark method returns something is
        // because without this JIT can remove the below statement at all
        // during dead code elimination optimization.
        // https://benchmarkdotnet.org/articles/guides/good-practices.html#avoid-dead-code-elimination
        return _store.PendingTransactions[Txs[0].Id];
    }

    [Benchmark]
    public Transaction GetRecentTxOutOfManyTxs()
    {
        // Note that why this benchmark method returns something is
        // because without this JIT can remove the below statement at all
        // during dead code elimination optimization.
        // https://benchmarkdotnet.org/articles/guides/good-practices.html#avoid-dead-code-elimination
        return _store.PendingTransactions[Txs[TxsCount - 2].Id];
    }

    [Benchmark]
    public Transaction TryGetNonExistentTxId()
    {
        // Note that why this benchmark method returns something is
        // because without this JIT can remove the below statement at all
        // during dead code elimination optimization.
        // https://benchmarkdotnet.org/articles/guides/good-practices.html#avoid-dead-code-elimination
        return _store.PendingTransactions[default];
    }
}
