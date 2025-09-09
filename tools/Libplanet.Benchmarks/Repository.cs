using BenchmarkDotNet.Attributes;
using Libplanet.TestUtilities;
using Libplanet.Types;

namespace Libplanet.Benchmarks;

public class Repository
{
    private readonly int _blockCount;
    private readonly ImmutableArray<Block> _blocks;
    private readonly int _txCount;
    private readonly ImmutableArray<Transaction> _txs;
    private Data.Repository _repository = new();

    public Repository()
    {
        const int blockCount = 500;
        var seed = Random.Shared.Next();
        var random = new Random(seed);
        var proposer = Rand.Signer(random);
        var validators = Rand.ImmutableSortedSet(
            random,
            Rand.TestValidator,
            10);
        var genesisBlock = new GenesisBlockBuilder
        {
            Validators = [.. validators.Select(v => (Validator)v)],
        }.Create(proposer);
        var previousBlock = genesisBlock;
        var blockList = new List<Block>(blockCount)
        {
            genesisBlock,
        };
        var signer = Rand.Signer(random);
        var nonce = 0L;
        for (var i = 0; i < blockCount; i++)
        {
            var txList = new List<Transaction>();
            for (var j = 0; j < i % 5; j++)
            {
                txList.Add(new TransactionMetadata
                {
                    Nonce = nonce++,
                    Signer = signer.Address,
                    GenesisBlockHash = genesisBlock.BlockHash,
                    Timestamp = DateTimeOffset.UtcNow,
                    Actions = [],
                }.Sign(signer));
            }

            var block = new RawBlock
            {
                Header = new BlockHeader
                {
                    Height = previousBlock.Height + 1,
                    Timestamp = DateTimeOffset.UtcNow,
                    Proposer = proposer.Address,
                    PreviousBlockHash = previousBlock.BlockHash,
                },
                Content = new BlockContent
                {
                    Transactions = [.. txList],
                },
            }.Sign(proposer);

            blockList.Add(block);
            previousBlock = block;
        }

        _blocks = [.. blockList];
        _blockCount = _blocks.Length;
        _txs = [.. blockList.SelectMany(b => b.Transactions)];
        _txCount = _txs.Length;
    }

    [IterationSetup]
    public void InitializeFixture()
    {
        _repository = new();
    }

    [IterationCleanup]
    public void FinalizeFixture()
    {
        _repository.Clear();
    }

    [Benchmark]
    public void PutFirstEmptyBlock()
    {
        PutBlock(_blocks[0]);
    }

    [Benchmark]
    public void PutFirstBlockWithTxs()
    {
        PutBlock(_blocks[5]);
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
        var i = 0;
        foreach (var block in _blocks)
        {
            PutBlock(block);
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
        PutBlock(_blocks[_blockCount - 1]);
    }

    [Benchmark]
    public Block GetOldBlockOutOfManyBlocks()
    {
        // Note that why this benchmark method returns something is
        // because without this JIT can remove the below statement at all
        // during dead code elimination optimization.
        // https://benchmarkdotnet.org/articles/guides/good-practices.html#avoid-dead-code-elimination
        return _repository.GetBlock(_blocks[0].BlockHash);
    }

    [Benchmark]
    public Block GetRecentBlockOutOfManyBlocks()
    {
        // Note that why this benchmark method returns something is
        // because without this JIT can remove the below statement at all
        // during dead code elimination optimization.
        // https://benchmarkdotnet.org/articles/guides/good-practices.html#avoid-dead-code-elimination
        return _repository.GetBlock(_blocks[_blockCount - 2].BlockHash);
    }

    [Benchmark]
    public Block? TryGetNonExistentBlockHash()
    {
        // Note that why this benchmark method returns something is
        // because without this JIT can remove the below statement at all
        // during dead code elimination optimization.
        // https://benchmarkdotnet.org/articles/guides/good-practices.html#avoid-dead-code-elimination
        if (_repository.TryGetBlock(blockHash: default, out var block))
        {
            return block;
        }

        return null;
    }

    [Benchmark]
    public void PutFirstTx()
    {
        _repository.CommittedTransactions.Add(_txs[0]);
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
        var i = 0;
        foreach (var tx in _txs)
        {
            _repository.CommittedTransactions.Add(tx);
            i++;
            if (i >= _txs.Length - 1)
            {
                break;
            }
        }
    }

    [Benchmark]
    public void PutTxOnManyTxs()
    {
        _repository.CommittedTransactions.Add(_txs[_txCount - 1]);
    }

    [Benchmark]
    public Transaction GetOldTxOutOfManyTxs()
    {
        // Note that why this benchmark method returns something is
        // because without this JIT can remove the below statement at all
        // during dead code elimination optimization.
        // https://benchmarkdotnet.org/articles/guides/good-practices.html#avoid-dead-code-elimination
        return _repository.CommittedTransactions[_txs[0].Id];
    }

    [Benchmark]
    public Transaction GetRecentTxOutOfManyTxs()
    {
        // Note that why this benchmark method returns something is
        // because without this JIT can remove the below statement at all
        // during dead code elimination optimization.
        // https://benchmarkdotnet.org/articles/guides/good-practices.html#avoid-dead-code-elimination
        return _repository.CommittedTransactions[_txs[_txCount - 2].Id];
    }

    [Benchmark]
    public Transaction? TryGetNonExistentTxId()
    {
        // Note that why this benchmark method returns something is
        // because without this JIT can remove the below statement at all
        // during dead code elimination optimization.
        // https://benchmarkdotnet.org/articles/guides/good-practices.html#avoid-dead-code-elimination
        if (_repository.CommittedTransactions.TryGetValue(key: default, out var tx))
        {
            return tx;
        }

        return null;
    }

    private void PutBlock(Block block)
    {
        _repository.BlockDigests.Add(block);
        _repository.BlockHashes.Add(block);
        _repository.CommittedTransactions.AddRange(block.Transactions);
        _repository.CommittedEvidences.AddRange(block.Evidences);
    }
}
