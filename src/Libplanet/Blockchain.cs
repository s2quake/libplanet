using System.Reactive.Subjects;
using System.Security.Cryptography;
using System.Reactive;
using Libplanet.State;
using Libplanet.Extensions;
using Libplanet.Data;
using Libplanet.Types;
using Microsoft.Extensions.Logging;

namespace Libplanet;

public partial class Blockchain
{
    private readonly Subject<Unit> _blockExecutingSubject = new();
    private readonly Subject<Block> _tipChangedSubject = new();
    private readonly Subject<(Block, BlockCommit)> _appendedSubject = new();
    private readonly Repository _repository;
    private readonly BlockExecutor _blockExecutor;
    private readonly ILogger<Blockchain> _logger;

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

    public Blockchain(Block genesisBlock, Repository repository)
        : this(genesisBlock, repository, new BlockchainOptions())
    {
    }

    public Blockchain(Block genesisBlock, Repository repository, BlockchainOptions options)
        : this(repository, options)
    {
        Append(genesisBlock, default);
    }

    public Blockchain(Repository repository, BlockchainOptions options)
    {
        _repository = repository;
        _blockExecutor = new BlockExecutor(repository.States, options.SystemActions);
        Options = options;
        _logger = options.Logger;
        Id = _repository.Id;
        BlockHashes = new BlockHashCollection(repository);
        Blocks = new BlockCollection(repository);
        BlockCommits = new BlockCommitCollection(repository);
        StagedTransactions = new StagedTransactionCollection(repository, options);
        Transactions = new TransactionCollection(repository);
        PendingEvidence = new PendingEvidenceCollection(repository, options);
        Evidence = new EvidenceCollection(repository);
        TxExecutions = new TxExecutionCollection(repository);
    }

    public IObservable<Unit> BlockExecuting => _blockExecutingSubject;

    public IObservable<ActionExecutionInfo> ActionExecuted => _blockExecutor.ActionExecuted;

    public IObservable<TransactionExecutionInfo> TransactionExecuted => _blockExecutor.TransactionExecuted;

    public IObservable<BlockExecutionInfo> BlockExecuted => _blockExecutor.BlockExecuted;

    public IObservable<Block> TipChanged => _tipChangedSubject;

    public IObservable<(Block, BlockCommit)> Appended => _appendedSubject;

    public Guid Id { get; }

    public BlockHashCollection BlockHashes { get; }

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
            throw new ArgumentException(
                $"Block {block.BlockHash} already exists in the store.", nameof(block));
        }

        if (_repository.BlockCommits.ContainsKey(block.BlockHash))
        {
            throw new ArgumentException(
                $"Block {block.BlockHash} already exists in the store.", nameof(blockCommit));
        }

        if (_repository.GenesisHeight != -1 && _repository.GenesisHeight != block.Height)
        {
            if (block.Version > _repository.BlockVersion)
            {
                throw new ArgumentException(
                    $"The protocol version ({block.Version}) of the block " +
                    $"#{block.Height} {block.BlockHash} is not supported by this node." +
                    $"The highest supported protocol version is {BlockHeader.CurrentProtocolVersion}.",
                    nameof(block));
            }

            if (block.Version < Tip.Version)
            {
                throw new ArgumentException(
                    $"The protocol version ({block.Version}) of the block " +
                    $"#{block.Height} {block.BlockHash} is lower than the current tip's version " +
                    $"({Tip.Version}). Downgrade of protocol version is not supported.",
                    nameof(block));
            }

            if (block.Height != Tip.Height + 1)
            {
                throw new ArgumentException(
                    "Block height does not match the current height + 1.",
                    nameof(block));
            }

            if (block.PreviousBlockHash != Tip.BlockHash)
            {
                throw new ArgumentException(
                    "Block's previous hash does not match the current tip's hash.",
                    nameof(block));
            }

            if (block.PreviousStateRootHash != StateRootHash)
            {
                throw new ArgumentException(
                    "Block's previous state root hash does not match the current state root hash.",
                    nameof(block));
            }

            if (block.Timestamp < Tip.Timestamp)
            {
                throw new ArgumentException(
                    "Block's timestamp is earlier than the current tip's timestamp.",
                    nameof(block));
            }

            if (block.PreviousBlockCommit != default)
            {
                var h = _repository.BlockDigests[block.PreviousBlockHash].Height;
                var v = this.GetValidators(h);
                try
                {
                    v.ValidateBlockCommitValidators(block.PreviousBlockCommit);
                }
                catch (Exception e)
                {
                    throw new ArgumentException(
                        "Block's previous commit is invalid.",
                        nameof(block), e);
                }
            }

            if (blockCommit == default)
            {
                throw new ArgumentException(
                    "Non-genesis block must have a block commit.",
                    nameof(blockCommit));
            }

            block.Validate(this);
            try
            {
                blockCommit.Validate(block);
            }
            catch (Exception e)
            {
                throw new ArgumentException("Block commit is invalid.", nameof(blockCommit), e);
            }

            foreach (var tx in block.Transactions)
            {
                try
                {
                    Options.TransactionOptions.Validate(tx);
                }
                catch (Exception e)
                {
                    throw new ArgumentException("Transaction is invalid.", nameof(block), e);
                }
            }

            var validators = this.GetValidators(block.Height);
            validators.ValidateBlockCommitValidators(blockCommit);

            BigInteger commitPower = blockCommit.Votes.Aggregate(
                BigInteger.Zero,
                (power, vote) => power + (vote.Type == VoteType.PreCommit
                    ? validators.GetValidator(vote.Validator).Power
                    : BigInteger.Zero));
            if (validators.GetTwoThirdsPower() >= commitPower)
            {
                throw new ArgumentException(
                    "Block commit does not have enough power.",
                    nameof(blockCommit));
            }

            Options.BlockOptions.Validate(block);
            foreach (var tx in block.Transactions)
            {
                Options.TransactionOptions.Validate(tx);
            }

            foreach (var evidence in block.Evidences)
            {
                Options.EvidenceOptions.Validate(block, evidence);
            }
        }

        _repository.Append(block, blockCommit);
        _tipChangedSubject.OnNext(block);
        _blockExecutingSubject.OnNext(Unit.Default);
        var execution = _blockExecutor.Execute((RawBlock)block);
        _repository.StateRootHash = execution.OutputWorld.Hash;
        _repository.StateRootHashes.Add(block.BlockHash, _repository.StateRootHash);
        _repository.TxExecutions.AddRange(execution.GetTxExecutions(block.BlockHash));
        _appendedSubject.OnNext((block, blockCommit));
        LogAppended(_logger, block.Height, block.BlockHash);
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

        var previousBlock = Blocks[block.PreviousBlockHash];
        var previousStateRootHash = _repository.StateRootHashes[previousBlock.BlockHash];
        if (previousStateRootHash != block.PreviousStateRootHash)
        {
            throw new InvalidOperationException(
                $"Block {block.BlockHash} has an invalid previous state root hash.");
        }
    }

    public World GetWorld() => GetWorld(Tip.BlockHash);

    public World GetWorld(int height) => GetWorld(Blocks[height].BlockHash);

    public World GetWorld(BlockHash blockHash)
        => new(_repository.States, _repository.StateRootHashes[blockHash]);

    public World GetWorld(HashDigest<SHA256> stateRootHash)
        => new(_repository.States.GetTrie(stateRootHash), _repository.States);

    public Block Propose(ISigner proposer)
    {
        var height = _repository.Height;
        var blockHash = _repository.BlockHashes[height];
        var blockCommit = height == _repository.GenesisHeight ? default : _repository.BlockCommits[blockHash];
        var timestamp = DateTimeOffset.UtcNow;
        var blockHeader = new BlockHeader
        {
            Height = height + 1,
            Timestamp = timestamp,
            Proposer = proposer.Address,
            PreviousBlockHash = blockHash,
            PreviousBlockCommit = blockCommit,
            PreviousStateRootHash = _repository.StateRootHashes[blockHash],
        };
        var blockContent = new BlockContent
        {
            Transactions = [.. StagedTransactions.Collect(timestamp)],
            Evidences = [.. PendingEvidence.Collect(height + 1)],
        };
        var rawBlock = new RawBlock
        {
            Header = blockHeader,
            Content = blockContent,
        };
        return rawBlock.Sign(proposer);
    }

    public Transaction CreateTransaction(ISigner signer, TransactionParams @params) => new TransactionMetadata
    {
        Nonce = @params.Nonce == -1L ? GetNextTxNonce(signer.Address) : @params.Nonce,
        Signer = signer.Address,
        GenesisBlockHash = Genesis.BlockHash,
        Actions = @params.Actions.ToBytecodes(),
        Timestamp = @params.Timestamp == default ? DateTimeOffset.UtcNow : @params.Timestamp,
        MaxGasPrice = @params.MaxGasPrice,
        GasLimit = @params.GasLimit,
    }.Sign(signer);
}
