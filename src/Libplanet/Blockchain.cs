using System.Reactive.Subjects;
using System.Security.Cryptography;
using System.Reactive;
using Libplanet.State;
using Libplanet.Extensions;
using Libplanet.Data;
using Libplanet.Types;

namespace Libplanet;

public partial class Blockchain
{
    private readonly Subject<Unit> _blockExecutingSubject = new();
    private readonly Subject<TipChangedInfo> _tipChangedSubject = new();
    private readonly Repository _repository;
    private readonly BlockExecutor _blockExecutor;

    public Blockchain()
        : this(new Repository(), BlockchainOptions.Empty)
    {
    }

    public Blockchain(BlockchainOptions options)
        : this(new Repository(), options)
    {
    }

    public Blockchain(Repository repository)
        : this(repository, BlockchainOptions.Empty)
    {
    }

    public Blockchain(Block genesisBlock)
        : this(genesisBlock, new Repository(), BlockchainOptions.Empty)
    {
    }

    public Blockchain(Block genesisBlock, BlockchainOptions options)
        : this(genesisBlock, new Repository(), options)
    {
    }

    public Blockchain(Block genesisBlock, Repository repository, BlockchainOptions options)
        : this(repository, options)
    {
        repository.GenesisHeight = genesisBlock.Height;
        Append(genesisBlock, BlockCommit.Empty);
    }

    public Blockchain(Repository repository, BlockchainOptions options)
    {
        _repository = repository;
        _blockExecutor = new BlockExecutor(repository.States, options.SystemActions);
        Options = options;
        Id = _repository.Id;
        Blocks = new BlockCollection(repository);
        BlockCommits = new BlockCommitCollection(repository);
        StagedTransactions = new StagedTransactionCollection(repository, options.TransactionOptions);
        Transactions = new TransactionCollection(repository);
        PendingEvidence = new PendingEvidenceCollection(repository);
        Evidence = new EvidenceCollection(repository);
        TxExecutions = new TxExecutionCollection(repository);
    }

    public IObservable<Unit> BlockExecuting => _blockExecutingSubject;

    public IObservable<ActionExecutionInfo> ActionExecuted => _blockExecutor.ActionExecuted;

    public IObservable<TransactionExecutionInfo> TransactionExecuted => _blockExecutor.TransactionExecuted;

    public IObservable<BlockExecutionInfo> BlockExecuted => _blockExecutor.BlockExecuted;

    public IObservable<TipChangedInfo> TipChanged => _tipChangedSubject;

    public Guid Id { get; }

    public BlockCollection Blocks { get; }

    public BlockCommitCollection BlockCommits { get; }

    public StagedTransactionCollection StagedTransactions { get; }

    public TransactionCollection Transactions { get; }

    public EvidenceCollection Evidence { get; }

    public PendingEvidenceCollection PendingEvidence { get; }

    public TxExecutionCollection TxExecutions { get; }

    public BlockInfo TipInfo { get; private set; } = BlockInfo.Empty;

    public Block Tip => Blocks.Count is not 0
        ? Blocks[^1] : throw new InvalidOperationException("The chain is empty.");

    public Block Genesis => Blocks.Count is not 0
        ? Blocks[0] : throw new InvalidOperationException("The chain is empty.");

    public HashDigest<SHA256> StateRootHash => Blocks.Count is not 0
        ? _repository.StateRootHash : throw new InvalidOperationException("The chain is empty.");

    public BlockCommit BlockCommit => Blocks.Count is not 0
        ? _repository.BlockCommit : throw new InvalidOperationException("The chain is empty.");

    private BlockchainOptions Options { get; }

    public long GetNextTxNonce(Address address) => StagedTransactions.GetNextTxNonce(address);

    internal long GetTxNonce(Address address) => _repository.GetNonce(address);

    public BlockExecutionInfo Execute(Block block)
    {
        var execution = _blockExecutor.Execute((RawBlock)block);
        var blockHash = block.BlockHash;
        _repository.TxExecutions.AddRange(execution.GetTxExecutions(blockHash));
        _repository.BlockExecutions.Add(execution.GetBlockExecution(blockHash));
        _repository.StateRootHashes.Add(blockHash, _repository.StateRootHash);

        return execution;
    }

    public void Append(Block block, BlockCommit blockCommit)
    {
        if (_repository.BlockDigests.ContainsKey(block.BlockHash))
        {
            throw new InvalidOperationException(
                $"Block {block.BlockHash} already exists in the store.");
        }

        if (_repository.BlockCommits.ContainsKey(block.BlockHash))
        {
            throw new InvalidOperationException(
                $"Block {block.BlockHash} already exists in the store.");
        }

        block.Validate(this);
        blockCommit.Validate(block);

        _repository.Append(block, blockCommit);
        _tipChangedSubject.OnNext(new(block));
        _blockExecutingSubject.OnNext(Unit.Default);
        var execution = _blockExecutor.Execute((RawBlock)block);
        _repository.StateRootHash = execution.OutputWorld.Hash;
        _repository.StateRootHashes.Add(block.BlockHash, _repository.StateRootHash);
        _repository.TxExecutions.AddRange(execution.GetTxExecutions(block.BlockHash));
    }

    public HashDigest<SHA256> GetStateRootHash(int height)
        => _repository.StateRootHashes[_repository.BlockHashes[height]];

    public HashDigest<SHA256> GetStateRootHash(BlockHash blockHash)
        => _repository.StateRootHashes[blockHash];

    public void Validate(Block block)
    {
        block.Validate(this);
        Options.BlockOptions.Validate(block);

        foreach (var tx in block.Transactions)
        {
            Options.TransactionOptions.Validate(tx);
        }
    }

    public World GetWorld() => GetWorld(Tip.BlockHash);

    public World GetWorld(int height) => GetWorld(Blocks[height].BlockHash);

    public World GetWorld(BlockHash blockHash)
        => new(_repository.States, _repository.StateRootHashes[blockHash]);

    public World GetWorld(HashDigest<SHA256> stateRootHash)
        => new(_repository.States.GetTrie(stateRootHash), _repository.States);

    public Block ProposeBlock(ISigner proposer)
    {
        var blockHeader = new BlockHeader
        {
            Height = _repository.Height + 1,
            Timestamp = DateTimeOffset.UtcNow,
            Proposer = proposer.Address,
            PreviousHash = _repository.BlockHash,
            PreviousCommit = _repository.BlockCommit,
            PreviousStateRootHash = _repository.StateRootHash,
        };
        var blockContent = new BlockContent
        {
            Transactions = StagedTransactions.Collect(Options.BlockOptions.MaxTransactionsPerBlock),
            Evidences = PendingEvidence.Collect(),
        };
        var rawBlock = new RawBlock
        {
            Header = blockHeader,
            Content = blockContent,
        };
        return rawBlock.Sign(proposer);
    }
}
