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
    private readonly BlockChainStates _blockChainStates;
    private readonly Repository _repository;
    private readonly Chain _chain;
    private readonly ActionEvaluator _actionEvaluator;

    private HashDigest<SHA256>? _nextStateRootHash;

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

        // var nonceDeltas = ValidateGenesisNonces(genesisBlock);

        // Blocks.AddCache(genesisBlock);

        // foreach (KeyValuePair<Address, long> pair in nonceDeltas)
        // {
        //     _chain.Nonces.Increase(pair.Key, pair.Value);
        // }

        // if (!Genesis.Equals(genesisBlock))
        // {
        //     string msg =
        //         $"The genesis block that the given {nameof(Libplanet.Store.Repository)} contains does not match " +
        //         "to the genesis block that the network expects.  You might pass the wrong " +
        //         "store which is incompatible with this chain.  Or your network might " +
        //         "restarted the chain with a new genesis block so that it is incompatible " +
        //         "with your existing chain in the local store.";
        //     throw new InvalidOperationException(
        //         message: msg);
        // }

        var block = _repository.GetBlock(_chain.BlockHash);
        var evaluation = _actionEvaluator.Evaluate((RawBlock)block, block.StateRootHash);
        _nextStateRootHash = evaluation.OutputWorld.Trie.Hash;
        // var txExecutions = MakeTxExecutions(Tip, actionEvaluations);
        _repository.TxExecutions.AddRange(evaluation.GetTxExecutions(block.BlockHash));
    }

    internal event EventHandler<(Block OldTip, Block NewTip)> TipChanged;

    public IObservable<RenderBlockInfo> RenderBlock => _renderBlock;

    public IObservable<ActionEvaluation> RenderAction => _actionEvaluator.ActionEvaluated;

    public IObservable<RenderBlockInfo> RenderBlockEnd => _renderBlockEnd;

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

    public IReadOnlyList<BlockHash> FindNextHashes(BlockHash locator, int count = 500)
    {
        if (!(FindBranchpoint(locator) is { } branchpoint))
        {
            return [];
        }

        if (!Blocks.TryGetValue(branchpoint, out var block))
        {
            return [];
        }

        var result = new List<BlockHash>();
        foreach (BlockHash hash in _repository.Chain.BlockHashes.IterateHeights(block.Height, count))
        {
            if (count == 0)
            {
                break;
            }

            result.Add(hash);
            count--;
        }

        return result;
    }

    public bool IsEvidenceExpired(EvidenceBase evidence)
        => evidence.Height + Options.EvidenceOptions.MaxEvidencePendingDuration + evidence.Height < Tip.Height;

    public void Append(Block block, BlockCommit blockCommit, bool validate = true)
    {
        if (Blocks.Count is 0)
        {
            throw new InvalidCastException(
                $"Cannot append block #{block.Height} {block.BlockHash} to an empty chain.");
        }

        if (block.Height is 0)
        {
            throw new ArgumentException(
                $"Cannot append genesis block #{block.Height} {block.BlockHash} to a chain.",
                nameof(block));
        }

        if (validate)
        {
            block.Header.Timestamp.ValidateTimestamp();
        }

        // _rwlock.EnterUpgradeableReadLock();
        Block prevTip = Tip;
        try
        {
            if (validate)
            {
                ValidateBlock(block);
                ValidateBlockCommit(block, blockCommit);
            }

            // var nonceDeltas = ValidateBlockNonces(
            //     block.Transactions
            //         .Select(tx => tx.Signer)
            //         .Distinct()
            //         .ToDictionary(signer => signer, signer => _chain.Nonces[signer]),
            //     block);

            if (validate)
            {
                Options.BlockOptions.Validator.Validate(block);
            }

            foreach (var tx in block.Transactions)
            {
                if (validate)
                {
                    Options.TransactionOptions.Validator.Validate(tx);
                }
            }

            try
            {
                if (validate)
                {
                    ValidateBlockStateRootHash(block);
                }

                Blocks.AddCache(block);

                _repository.Append(block, blockCommit);
                _repository.Chain.Append(block, blockCommit);
                _nextStateRootHash = null;
            }
            finally
            {
            }

            TipChanged?.Invoke(this, (prevTip, block));
            _renderBlock.OnNext(new RenderBlockInfo(prevTip ?? Genesis, block));
            // _nextStateRootHash = DetermineNextBlockStateRootHash(block, out var actionEvaluations);
            _renderBlockEnd.OnNext(new RenderBlockInfo(prevTip ?? Genesis, block));

            // var txExecutions = MakeTxExecutions(block, actionEvaluations);
            // _repository.TxExecutions.AddRange(txExecutions);
        }
        finally
        {
        }
    }

    internal BlockHash? FindBranchpoint(BlockHash blockHash)
    {
        if (Blocks.ContainsKey(blockHash))
        {
            return blockHash;
        }

        return null;
    }

    // internal void CleanupBlockCommitStore(long limit)
    // {
    //     // FIXME: This still isn't enough to prevent the canonical chain
    //     // removing cached block commits that are needed by other non-canonical chains.
    //     if (!IsCanonical)
    //     {
    //         throw new InvalidOperationException(
    //             $"Cannot perform {nameof(CleanupBlockCommitStore)}() from a " +
    //             "non canonical chain.");
    //     }

    //     List<BlockHash> hashes = _repository.BlockCommits.Keys.ToList();

    //     foreach (var hash in hashes)
    //     {
    //         if (_repository.BlockCommits.TryGetValue(hash, out var commit) && commit.Height < limit)
    //         {
    //             _repository.BlockCommits.Remove(hash);
    //         }
    //     }
    // }

    internal HashDigest<SHA256>? GetNextStateRootHash() => _nextStateRootHash;

    internal HashDigest<SHA256>? GetNextStateRootHash(int index) => GetNextStateRootHash(Blocks[index]);

    internal HashDigest<SHA256>? GetNextStateRootHash(BlockHash blockHash) => GetNextStateRootHash(Blocks[blockHash]);

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

    private HashDigest<SHA256>? GetNextStateRootHash(Block block)
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
