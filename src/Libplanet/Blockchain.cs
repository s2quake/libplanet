using System.Reactive.Subjects;
using System.Security.Cryptography;
using Libplanet.State;
using Libplanet.Extensions;
using Libplanet.Data;
using Libplanet.Types;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Libplanet.Types.Crypto;
using Libplanet.Types.Evidence;
using Libplanet.Types.Transactions;
using static Libplanet.State.SystemAddresses;

namespace Libplanet;

public partial class Blockchain
{
    private readonly Subject<RenderBlockInfo> _blockEvaluating = new();
    private readonly Subject<RenderBlockInfo> _blockEvaluated = new();
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

    public Blockchain(Block genesisBlock, Repository repository, BlockchainOptions options)
        : this(repository, options)
    {
        var evaluation = Evaluate(genesisBlock);
        _repository.Append(genesisBlock, BlockCommit.Empty);
        _repository.StateRootHash = evaluation.OutputWorld.Hash;
    }

    public Blockchain(Repository repository, BlockchainOptions options)
    {
        _repository = repository;
        _blockExecutor = new BlockExecutor(repository.StateStore, options.PolicyActions);
        Options = options;
        Id = _repository.Id;
        Blocks = new BlockCollection(repository);
        BlockCommits = new BlockCommitCollection(repository);
        StagedTransactions = new StagedTransactionCollection(repository);
        Transactions = new TransactionCollection(repository);
        PendingEvidences = new PendingEvidenceCollection(repository);
        Evidences = new EvidenceCollection(repository);
        TxExecutions = new TxExecutionCollection(repository);
    }

    public IObservable<RenderBlockInfo> RenderBlock => _blockEvaluating;

    public IObservable<ActionResult> RenderAction => _blockExecutor.ActionExecuted;

    public IObservable<RenderBlockInfo> RenderBlockEnd => _blockEvaluated;

    public IObservable<TipChangedInfo> TipChanged => _tipChangedSubject;

    public Guid Id { get; }

    public BlockCollection Blocks { get; }

    public BlockCommitCollection BlockCommits { get; }

    public StagedTransactionCollection StagedTransactions { get; }

    public TransactionCollection Transactions { get; }

    public EvidenceCollection Evidences { get; }

    public PendingEvidenceCollection PendingEvidences { get; }

    public TxExecutionCollection TxExecutions { get; }

    public Block Tip => Blocks.Count is not 0
        ? Blocks[^1] : throw new InvalidOperationException("The chain is empty.");

    public Block Genesis => Blocks.Count is not 0
        ? Blocks[0] : throw new InvalidOperationException("The chain is empty.");

    public HashDigest<SHA256> StateRootHash => Blocks.Count is not 0
        ? _repository.StateRootHash : throw new InvalidOperationException("The chain is empty.");

    public BlockCommit BlockCommit => Blocks.Count is not 0
        ? _repository.BlockCommit : throw new InvalidOperationException("The chain is empty.");

    public BlockchainOptions Options { get; }

    public long GetNextTxNonce(Address address) => StagedTransactions.GetNextTxNonce(address);

    public bool IsEvidenceExpired(EvidenceBase evidence)
        => evidence.Height + Options.EvidenceOptions.MaxEvidencePendingDuration + evidence.Height < Tip.Height;

    public BlockResult Evaluate(Block block)
    {
        var evaluation = _blockExecutor.Execute((RawBlock)block);
        var blockHash = block.BlockHash;
        _repository.TxExecutions.AddRange(evaluation.GetTxExecutions(blockHash));
        _repository.BlockExecutions.Add(blockHash, evaluation.GetBlockExecution(blockHash));
        _repository.StateRootHashStore.Add(blockHash, _repository.StateRootHash);

        return evaluation;
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

        var oldTip = Tip;
        block.Validate(this);
        blockCommit.Validate(block);

        _repository.Append(block, blockCommit);
        _tipChangedSubject.OnNext(new(oldTip, block));
        _blockEvaluating.OnNext(new RenderBlockInfo(oldTip, block));
        var evaluation = _blockExecutor.Execute((RawBlock)block);
        _blockEvaluated.OnNext(new RenderBlockInfo(oldTip, block));
        _repository.StateRootHash = evaluation.OutputWorld.Hash;
        _repository.StateRootHashStore.Add(block.BlockHash, _repository.StateRootHash);
        _repository.TxExecutions.AddRange(evaluation.GetTxExecutions(block.BlockHash));
    }

    public HashDigest<SHA256> GetStateRootHash(int height)
        => _repository.StateRootHashStore[_repository.BlockHashes[height]];

    public HashDigest<SHA256> GetStateRootHash(BlockHash blockHash)
        => _repository.StateRootHashStore[blockHash];

    public World GetWorld() => GetWorld(Tip.BlockHash);

    public World GetWorld(int height) => GetWorld(Blocks[height].BlockHash);

    public World GetWorld(BlockHash blockHash)
    {
        var stateRootHash = _repository.StateRootHashStore[blockHash];
        return new World(_repository.StateStore.GetStateRoot(stateRootHash), _repository.StateStore);
    }

    public World GetWorld(HashDigest<SHA256> stateRootHash)
    {
        return new World(_repository.StateStore.GetStateRoot(stateRootHash), _repository.StateStore);
    }

    public static Block ProposeGenesisBlock(
        PrivateKey proposer,
        ImmutableSortedSet<Transaction> transactions,
        HashDigest<SHA256> previousStateRootHash = default)
    {
        var blockHeader = new BlockHeader
        {
            Timestamp = DateTimeOffset.UtcNow,
            Proposer = proposer.Address,
            PreviousStateRootHash = previousStateRootHash,
        };
        var blockContent = new BlockContent
        {
            Transactions = transactions,
            Evidences = [],
        };
        var rawBlock = new RawBlock
        {
            Header = blockHeader,
            Content = blockContent,
        };
        return rawBlock.Sign(proposer);
    }

    public static Block ProposeGenesisBlock(
        PrivateKey proposer,
        ImmutableArray<IAction> actions,
        HashDigest<SHA256> previousStateRootHash = default)
    {
        var transaction = new TransactionMetadata
        {
            Signer = proposer.Address,
            Actions = actions.ToBytecodes(),
            Timestamp = DateTimeOffset.UtcNow,
        }.Sign(proposer);
        var blockHeader = new BlockHeader
        {
            Timestamp = DateTimeOffset.UtcNow,
            Proposer = proposer.Address,
            PreviousStateRootHash = previousStateRootHash,
        };
        var blockContent = new BlockContent
        {
            Transactions = [transaction],
            Evidences = [],
        };
        var rawBlock = new RawBlock
        {
            Header = blockHeader,
            Content = blockContent,
        };
        return rawBlock.Sign(proposer);
    }

    public Block ProposeBlock(PrivateKey proposer)
    {
        var tip = Tip;
        var height = tip.Height + 1;
        var transactions = StagedTransactions.Collect();
        var evidences = PendingEvidences.Collect();
        var previousHash = tip.BlockHash;
        var blockHeader = new BlockHeader
        {
            Height = height,
            Timestamp = DateTimeOffset.UtcNow,
            Proposer = proposer.Address,
            PreviousHash = previousHash,
            PreviousCommit = BlockCommits[previousHash],
            PreviousStateRootHash = GetStateRootHash(previousHash),
        };
        var blockContent = new BlockContent
        {
            Transactions = transactions,
            Evidences = evidences,
        };
        var rawBlock = new RawBlock
        {
            Header = blockHeader,
            Content = blockContent,
        };
        return rawBlock.Sign(proposer);
    }
}
