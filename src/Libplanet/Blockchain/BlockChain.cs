using System.Reactive.Subjects;
using System.Security.Cryptography;
using Libplanet.State;
using Libplanet.Blockchain.Extensions;
using Libplanet.Data;
using Libplanet.Types;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Libplanet.Types.Crypto;
using Libplanet.Types.Evidence;
using static Libplanet.State.SystemAddresses;

namespace Libplanet.Blockchain;

public partial class BlockChain
{
    private readonly Subject<RenderBlockInfo> _blockEvaluating = new();
    private readonly Subject<RenderBlockInfo> _blockEvaluated = new();
    private readonly Subject<TipChangedInfo> _tipChangedSubject = new();
    private readonly Repository _repository;
    private readonly BlockExecutor _actionEvaluator;

    public BlockChain()
        : this(new Repository(), BlockChainOptions.Empty)
    {
    }

    public BlockChain(BlockChainOptions options)
        : this(new Repository(), options)
    {
    }

    public BlockChain(Repository repository)
        : this(repository, BlockChainOptions.Empty)
    {
    }

    public BlockChain(Block genesisBlock)
        : this(genesisBlock, new Repository(), BlockChainOptions.Empty)
    {
    }

    public BlockChain(Block genesisBlock, Repository repository, BlockChainOptions options)
        : this(repository, options)
    {
        var evaluation = Evaluate(genesisBlock);
        _repository.Append(genesisBlock, BlockCommit.Empty);
        // _chain.Append(genesisBlock, BlockCommit.Empty);
        _repository.StateRootHash = evaluation.OutputWorld.Hash;
    }

    public BlockChain(Repository repository, BlockChainOptions options)
    {
        _repository = repository;
        // _chain = repository.Chain;
        _actionEvaluator = new BlockExecutor(repository.StateStore, options.PolicyActions);
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

    public IObservable<ActionResult> RenderAction => _actionEvaluator.ActionExecuted;

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

    public BlockChainOptions Options { get; }

    public long GetNextTxNonce(Address address) => StagedTransactions.GetNextTxNonce(address);

    public bool IsEvidenceExpired(EvidenceBase evidence)
        => evidence.Height + Options.EvidenceOptions.MaxEvidencePendingDuration + evidence.Height < Tip.Height;

    public BlockResult Evaluate(Block block)
    {
        var evaluation = _actionEvaluator.Execute((RawBlock)block);
        var blockHash = block.BlockHash;
        _repository.TxExecutions.AddRange(evaluation.GetTxExecutions(blockHash));
        _repository.BlockExecutions.Add(blockHash, evaluation.GetBlockExecution(blockHash));
        _repository.StateRootHashStore.Add(blockHash, _repository.StateRootHash);

        return evaluation;
    }

    public void Append(Block block, BlockCommit blockCommit)
    {
        // if (Blocks.Count is 0)
        // {
        //     throw new InvalidCastException(
        //         $"Cannot append block #{block.Height} {block.BlockHash} to an empty chain.");
        // }

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

        // ValidateBlockStateRootHash(block);

        _repository.Append(block, blockCommit);
        // _chain.Append(block, blockCommit);
        // Blocks.AddCache(block);

        _tipChangedSubject.OnNext(new(oldTip, block));
        _blockEvaluating.OnNext(new RenderBlockInfo(oldTip, block));
        var evaluation = _actionEvaluator.Execute((RawBlock)block);
        _blockEvaluated.OnNext(new RenderBlockInfo(oldTip, block));
        _repository.StateRootHash = evaluation.OutputWorld.Hash;
        _repository.StateRootHashStore.Add(block.BlockHash, _repository.StateRootHash);
        _repository.TxExecutions.AddRange(evaluation.GetTxExecutions(block.BlockHash));
    }

    public HashDigest<SHA256> GetStateRootHash(int height)
        => _repository.StateRootHashStore[_repository.BlockHashes[height]];

    public HashDigest<SHA256> GetStateRootHash(BlockHash blockHash)
        => _repository.StateRootHashStore[blockHash];

    // internal ImmutableSortedSet<Validator> GetValidatorSet(int height)
    // {
    //     return GetWorld(height).GetValidatorSet();
    // }

    // private static Repository CreateChain(Block genesisBlock, Repository repository)
    // {
    //     repository.AddNewChain(genesisBlock);
    //     return repository;
    // }
}
