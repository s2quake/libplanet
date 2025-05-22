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
    private readonly Subject<TipChangedInfo> _tipChangedSubject = new();
    private readonly Repository _repository;
    private readonly Chain _chain;
    private readonly ActionEvaluator _actionEvaluator;

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
        var evaluation = _actionEvaluator.Evaluate((RawBlock)genesisBlock);
        _repository.Append(genesisBlock, BlockCommit.Empty);
        _chain.Append(genesisBlock, BlockCommit.Empty);
        _chain.StateRootHash = evaluation.OutputWorld.Trie.Hash;
        _repository.TxExecutions.AddRange(evaluation.GetTxExecutions(genesisBlock.BlockHash));
        _repository.StateRootHashStore.Add(genesisBlock.BlockHash, _chain.StateRootHash);
    }

    public BlockChain(Repository repository, BlockChainOptions options)
    {
        _repository = repository;
        _chain = repository.Chain;
        _actionEvaluator = new ActionEvaluator(repository.StateStore, options.PolicyActions);
        Options = options;
        Id = _repository.ChainId;
        Blocks = new BlockCollection(repository);
        BlockCommits = new BlockCommitCollection(repository);
        StagedTransactions = new StagedTransactionCollection(repository);
        Transactions = new TransactionCollection(repository);
        PendingEvidences = new PendingEvidenceCollection(repository);
        Evidences = new EvidenceCollection(repository);
        TxExecutions = new TxExecutionCollection(repository);
    }

    public IObservable<RenderBlockInfo> RenderBlock => _renderBlock;

    public IObservable<ActionEvaluation> RenderAction => _actionEvaluator.ActionEvaluated;

    public IObservable<RenderBlockInfo> RenderBlockEnd => _renderBlockEnd;

    public IObservable<TipChangedInfo> TipChanged => _tipChangedSubject;

    public Guid Id { get; }

    public BlockCollection Blocks { get; }

    public BlockCommitCollection BlockCommits { get; }

    public StagedTransactionCollection StagedTransactions { get; }

    public TransactionCollection Transactions { get; }

    public EvidenceCollection Evidences { get; }

    public PendingEvidenceCollection PendingEvidences { get; }

    public TxExecutionCollection TxExecutions{ get; }

    public Block Tip => Blocks.Count is not 0
        ? Blocks[^1] : throw new InvalidOperationException("The chain is empty.");

    public Block Genesis => Blocks.Count is not 0
        ? Blocks[0] : throw new InvalidOperationException("The chain is empty.");

    public HashDigest<SHA256> StateRootHash => Blocks.Count is not 0
        ? _chain.StateRootHash : throw new InvalidOperationException("The chain is empty.");

    public BlockCommit BlockCommit => Blocks.Count is not 0
        ? _chain.BlockCommit : throw new InvalidOperationException("The chain is empty.");

    public BlockChainOptions Options { get; }

    public long GetNextTxNonce(Address address) => StagedTransactions.GetNextTxNonce(address);

    public bool IsEvidenceExpired(EvidenceBase evidence)
        => evidence.Height + Options.EvidenceOptions.MaxEvidencePendingDuration + evidence.Height < Tip.Height;

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
        _chain.Append(block, blockCommit);
        Blocks.AddCache(block);

        _tipChangedSubject.OnNext(new(oldTip, block));
        _renderBlock.OnNext(new RenderBlockInfo(oldTip, block));
        var evaluation = _actionEvaluator.Evaluate((RawBlock)block);
        _renderBlockEnd.OnNext(new RenderBlockInfo(oldTip, block));
        _chain.StateRootHash = evaluation.OutputWorld.Trie.Hash;
        _repository.TxExecutions.AddRange(evaluation.GetTxExecutions(block.BlockHash));
        _repository.StateRootHashStore.Add(block.BlockHash, _chain.StateRootHash);
    }

    public HashDigest<SHA256> GetStateRootHash(int height)
        => _repository.StateRootHashStore[_chain.BlockHashes[height]];

    public HashDigest<SHA256> GetStateRootHash(BlockHash blockHash)
        => _repository.StateRootHashStore[blockHash];

    internal ImmutableSortedSet<Validator> GetValidatorSet(int height)
    {
        if (height == 0)
        {
            IWorldContext worldContext = new WorldStateContext(GetWorld());
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
}
