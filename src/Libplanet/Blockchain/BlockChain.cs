using System.Reactive.Subjects;
using System.Security.Cryptography;
using Libplanet.Action;
using Libplanet.Action.State;
using Libplanet.Blockchain.Extensions;
using Libplanet.Store;
using Libplanet.Types;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Libplanet.Types.Crypto;
using Libplanet.Types.Evidence;

namespace Libplanet.Blockchain;

public partial class BlockChain
{
    private readonly Subject<RenderBlockInfo> _renderBlock = new();
    private readonly Subject<RenderBlockInfo> _renderBlockEnd = new();
    private readonly Subject<TipChangedEvent> _tipChangedSubject = new();
    private readonly BlockChainStates _blockChainStates;
    private readonly Repository _repository;
    private readonly Chain _chain;
    private readonly ActionEvaluator _actionEvaluator;

    private HashDigest<SHA256> _nextStateRootHash;

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

    public BlockChain(Block genesisBlock, Repository repository, BlockChainOptions options)
        : this(CreateChain(genesisBlock, repository), options)
    {
    }

    public BlockChain(Repository repository, BlockChainOptions options)
    {
        Options = options;
        _repository = repository;
        _chain = repository.Chain;
        _blockChainStates = new BlockChainStates(repository);
        _actionEvaluator = new ActionEvaluator(repository.StateStore, options.PolicyActions);
        Id = _repository.ChainId;
        StagedTransactions = new StagedTransactionCollection(repository);
        Transactions = new TransactionCollection(repository);
        PendingEvidences = new PendingEvidenceCollection(repository);
        Evidences = new EvidenceCollection(repository);
        Blocks = new BlockCollection(repository);
        BlockCommits = new BlockCommitCollection(repository);

        var block = _repository.GetBlock(_chain.BlockHash);
        var evaluation = _actionEvaluator.Evaluate((RawBlock)block, block.StateRootHash);
        _nextStateRootHash = evaluation.OutputWorld.Trie.Hash;
        _repository.TxExecutions.AddRange(evaluation.GetTxExecutions(block.BlockHash));
    }

    public IObservable<RenderBlockInfo> RenderBlock => _renderBlock;

    public IObservable<ActionEvaluation> RenderAction => _actionEvaluator.ActionEvaluated;

    public IObservable<RenderBlockInfo> RenderBlockEnd => _renderBlockEnd;

    public IObservable<TipChangedEvent> TipChanged => _tipChangedSubject;

    public StagedTransactionCollection StagedTransactions { get; }

    public TransactionCollection Transactions { get; }

    public EvidenceCollection Evidences { get; }

    public PendingEvidenceCollection PendingEvidences { get; }

    public Block Tip => Blocks[^1];

    public Block Genesis => Blocks[0];

    public BlockChainOptions Options { get; }

    public BlockCollection Blocks { get; }

    public BlockCommitCollection BlockCommits { get; }

    public TxExecutionStore TxExecutions => _repository.TxExecutions;

    public Guid Id { get; }

    public long GetNextTxNonce(Address address) => StagedTransactions.GetNextTxNonce(address);

    public bool IsEvidenceExpired(EvidenceBase evidence)
        => evidence.Height + Options.EvidenceOptions.MaxEvidencePendingDuration + evidence.Height < Tip.Height;

    public void Append(Block block, BlockCommit blockCommit)
    {
        if (Blocks.Count is 0)
        {
            throw new InvalidCastException(
                $"Cannot append block #{block.Height} {block.BlockHash} to an empty chain.");
        }

        var oldTip = Tip;
        block.Validate(this);
        blockCommit.Validate(block);

        ValidateBlockStateRootHash(block);

        _repository.Append(block, blockCommit);
        _chain.Append(block, blockCommit);
        Blocks.AddCache(block);
        _nextStateRootHash = default;

        _tipChangedSubject.OnNext(new(oldTip, block));
        _renderBlock.OnNext(new RenderBlockInfo(oldTip, block));
        var evaluation = _actionEvaluator.Evaluate((RawBlock)block, block.StateRootHash);
        _renderBlockEnd.OnNext(new RenderBlockInfo(oldTip, block));
        _nextStateRootHash = evaluation.OutputWorld.Trie.Hash;
        _repository.TxExecutions.AddRange(evaluation.GetTxExecutions(block.BlockHash));
    }

    internal HashDigest<SHA256> GetNextStateRootHash() => _nextStateRootHash;

    internal HashDigest<SHA256> GetNextStateRootHash(int height) => GetNextStateRootHash(Blocks[height]);

    internal HashDigest<SHA256> GetNextStateRootHash(BlockHash blockHash) => GetNextStateRootHash(Blocks[blockHash]);

    internal ImmutableSortedSet<Validator> GetValidatorSet(int height)
    {
        if (height == 0)
        {
            IWorldContext worldContext = new WorldStateContext(GetNextWorld());
            return (ImmutableSortedSet<Validator>)worldContext[ReservedAddresses.ValidatorSetAddress][ReservedAddresses.ValidatorSetAddress]; ;
        }

        var blockCommit = BlockCommits[height];
        return [.. blockCommit.Votes.Select(CreateValidator)];

        static Validator CreateValidator(Vote vote)
            => Validator.Create(vote.Validator, vote.ValidatorPower);
    }

    private static Repository CreateChain(Block genesisBlock, Repository repository)
    {
        repository.AddNewChain(genesisBlock);
        return repository;
    }

    private HashDigest<SHA256> GetNextStateRootHash(Block block)
    {
        if (block.Height < Tip.Height)
        {
            return Blocks[block.Height + 1].StateRootHash;
        }
        else
        {
            return GetNextStateRootHash();
        }
    }
}
