using System.Diagnostics;
using System.Globalization;
using System.Reactive.Subjects;
using System.Security.Cryptography;
using System.Threading;
using Libplanet.Action;
using Libplanet.Action.State;
using Libplanet.Store;
using Libplanet.Types;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Libplanet.Types.Crypto;
using Libplanet.Types.Evidence;
using Libplanet.Types.Tx;
using Serilog;

namespace Libplanet.Blockchain;

public partial class BlockChain
{
    internal readonly ReaderWriterLockSlim _rwlock;
    private readonly object _txLock;
    private readonly ILogger _logger;
    private readonly Subject<RenderBlockInfo> _renderBlock = new();
    private readonly Subject<RenderActionInfo> _renderAction = new();
    private readonly Subject<RenderBlockInfo> _renderBlockEnd = new();
    private readonly BlockChainStates _blockChainStates;
    private readonly Chain _chain;

    private Block _genesisBlock;

    private HashDigest<SHA256>? _nextStateRootHash;

    public BlockChain(Block genesisBlock, BlockChainOptions options)
        : this(genesisBlock, options.Store.ChainId, options)
    {
    }

    private BlockChain(Block genesisBlock, Guid id, BlockChainOptions options)
    {
        if (options.Store.ChainId is { } canonId && canonId != Guid.Empty)
        {
            throw new ArgumentException(
                $"Given {nameof(options.Store)} already has its canonical chain id set: {canonId}",
                nameof(options));
        }

        genesisBlock.ValidateAsGenesis();

        Id = id;
        Options = options;
        StagedTransactions = new StagedTransactionCollection(options.Store);
        Transactions = new TransactionCollection(options.Store);
        PendingEvidences = new PendingEvidenceCollection(options.Store);
        Evidences = new EvidenceCollection(options.Store);
        Store = options.Store;
        StateStore = new TrieStateStore(options.KeyValueStore);
        _chain = Store.Chains.GetOrAdd(id);
        Store.ChainId = id;
        Blocks = new BlockCollection(options.Store, id);
        Nonces = _chain.Nonces;

        var nonceDeltas = ValidateGenesisNonces(genesisBlock);

        Blocks.Add(genesisBlock);

        foreach (KeyValuePair<Address, long> pair in nonceDeltas)
        {
            Nonces.Increase(pair.Key, pair.Value);
        }

        _blockChainStates = new BlockChainStates(options.Store, StateStore);

        _rwlock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        _txLock = new object();

        _logger = Log
            .ForContext<BlockChain>()
            .ForContext("Source", nameof(BlockChain))
            .ForContext("ChainId", Id);
        ActionEvaluator = new ActionEvaluator(StateStore, options.PolicyActions);

        if (!Genesis.Equals(genesisBlock))
        {
            string msg =
                $"The genesis block that the given {nameof(Libplanet.Store.Repository)} contains does not match " +
                "to the genesis block that the network expects.  You might pass the wrong " +
                "store which is incompatible with this chain.  Or your network might " +
                "restarted the chain with a new genesis block so that it is incompatible " +
                "with your existing chain in the local store.";
            throw new InvalidOperationException(
                message: msg);
        }

        _nextStateRootHash =
            DetermineNextBlockStateRootHash(Tip, out var actionEvaluations);
        IEnumerable<TxExecution> txExecutions = MakeTxExecutions(Tip, actionEvaluations);
        Store.TxExecutions.AddRange(txExecutions);
    }

    ~BlockChain()
    {
        _rwlock?.Dispose();
    }

    internal event EventHandler<(Block OldTip, Block NewTip)> TipChanged;

    public IObservable<RenderBlockInfo> RenderBlock => _renderBlock;

    public IObservable<RenderActionInfo> RenderAction => _renderAction;

    public IObservable<RenderBlockInfo> RenderBlockEnd => _renderBlockEnd;

    public BlockChainOptions Options { get; }

    public StagedTransactionCollection StagedTransactions { get; }

    public TransactionCollection Transactions { get; }

    public EvidenceCollection Evidences { get; }

    public PendingEvidenceCollection PendingEvidences { get; }

    public Block Tip => Blocks[^1];

    public Block Genesis => _genesisBlock ??= Blocks[0];

    public Guid Id { get; private set; }

    internal Store.Repository Store { get; }

    internal TrieStateStore StateStore { get; }

    internal ActionEvaluator ActionEvaluator { get; }

    public BlockCollection Blocks { get; }

    public NonceStore Nonces { get; }

    public TxExecutionStore TxExecutions => Store.TxExecutions;

    internal bool IsCanonical => Store.ChainId is Guid guid && Id == guid;

    public static BlockChain Create(Block genesisBlock, BlockChainOptions options)
    {
        return new BlockChain(genesisBlock, Guid.NewGuid(), options);
    }

    public void Append(Block block, BlockCommit blockCommit, bool validate = true)
    {
        Append(block, blockCommit, render: true, validate: validate);
    }

    public long GetNextTxNonce(Address address) => StagedTransactions.GetNextTxNonce(address);

    public Transaction MakeTransaction(
        PrivateKey privateKey,
        IEnumerable<IAction> actions,
        FungibleAssetValue? maxGasPrice = null,
        long gasLimit = 0L,
        DateTimeOffset? timestamp = null)
    {
        lock (_txLock)
        {
            var tx = new TransactionMetadata
            {
                Nonce = GetNextTxNonce(privateKey.Address),
                Signer = privateKey.Address,
                GenesisHash = Genesis.BlockHash,
                Actions = actions.ToBytecodes(),
                MaxGasPrice = maxGasPrice,
                GasLimit = gasLimit,
                Timestamp = timestamp ?? DateTimeOffset.UtcNow,
            }.Sign(privateKey);
            StagedTransactions.Add(tx);
            return tx;
        }
    }

    public IReadOnlyList<BlockHash> FindNextHashes(BlockLocator locator, int count = 500)
    {
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        if (!(FindBranchpoint(locator) is { } branchpoint))
        {
            return Array.Empty<BlockHash>();
        }

        if (!Blocks.TryGetValue(branchpoint, out var block))
        {
            return Array.Empty<BlockHash>();
        }

        var result = new List<BlockHash>();
        foreach (BlockHash hash in Store.Chains.GetOrAdd(Id).BlockHashes.IterateHeights(block.Height, count))
        {
            if (count == 0)
            {
                break;
            }

            result.Add(hash);
            count--;
        }

        _logger
            .ForContext("Tag", "Metric")
            .ForContext("Subtag", "FindHashesDuration")
            .Information(
                "Found {HashCount} hashes from storage with {ChainIdCount} chain ids " +
                "in {DurationMs} ms",
                result.Count,
                Store.Chains.Keys.Count,
                stopwatch.ElapsedMilliseconds);

        return result;
    }

    public BlockLocator GetBlockLocator()
    {
        _rwlock.EnterReadLock();
        try
        {
            return new BlockLocator(Tip.BlockHash);
        }
        finally
        {
            _rwlock.ExitReadLock();
        }
    }

    public BlockCommit GetBlockCommit(int index)
    {
        Block block = Blocks[index];

        // if (block.ProtocolVersion < BlockHeader.PBFTProtocolVersion)
        // {
        //     return null;
        // }

        return index == Tip.Height
            ? Store.Chains[Id].BlockCommit
            : Blocks[index + 1].LastCommit;
    }

    public BlockCommit GetBlockCommit(BlockHash blockHash) => Store.BlockCommits[blockHash];

    public bool IsEvidenceExpired(EvidenceBase evidence)
        => evidence.Height + Options.MaxEvidencePendingDuration + evidence.Height < Tip.Height;

    internal void Append(
        Block block,
        BlockCommit blockCommit,
        bool render,
        bool validate = true)
    {
        if (Blocks.Count is 0)
        {
            throw new ArgumentException(
                "Cannot append a block to an empty chain.");
        }
        else if (block.Height is 0)
        {
            throw new ArgumentException(
                $"Cannot append genesis block #{block.Height} {block.BlockHash} to a chain.",
                nameof(block));
        }

        _logger.Information(
            "Trying to append block #{BlockHeight} {BlockHash}...", block.Height, block.BlockHash);

        if (validate)
        {
            block.Header.Timestamp.ValidateTimestamp();
        }

        _rwlock.EnterUpgradeableReadLock();
        Block prevTip = Tip;
        try
        {
            if (validate)
            {
                ValidateBlock(block);
                ValidateBlockCommit(block, blockCommit);
            }

            var nonceDeltas = ValidateBlockNonces(
                block.Transactions
                    .Select(tx => tx.Signer)
                    .Distinct()
                    .ToDictionary(signer => signer, signer => Nonces[signer]),
                block);

            if (validate)
            {
                ValidateBlockLoadActions(block);
            }

            if (validate)
            {
                Options.BlockValidation?.Invoke(this, block);
            }

            foreach (Transaction tx in block.Transactions)
            {
                if (validate)
                {
                    Options.ValidateTransaction(this, tx);
                }
            }

            _rwlock.EnterWriteLock();
            try
            {
                if (validate)
                {
                    ValidateBlockStateRootHash(block);
                }

                // FIXME: Using evaluateActions as a proxy flag for preloading status.
                const string TimestampFormat = "yyyy-MM-ddTHH:mm:ss.ffffffZ";
                _logger
                    .ForContext("Tag", "Metric")
                    .ForContext("Subtag", "BlockAppendTimestamp")
                    .Information(
                        "Block #{BlockHeight} {BlockHash} with " +
                        "timestamp {BlockTimestamp} appended at {AppendTimestamp}",
                        block.Height,
                        block.BlockHash,
                        block.Timestamp.ToString(
                            TimestampFormat, CultureInfo.InvariantCulture),
                        DateTimeOffset.UtcNow.ToString(
                            TimestampFormat, CultureInfo.InvariantCulture));

                Blocks[block.BlockHash] = block;

                foreach (KeyValuePair<Address, long> pair in nonceDeltas)
                {
                    Nonces.Increase(pair.Key, pair.Value);
                }

                // foreach (var tx in block.Transactions)
                // {
                //     Store.BlockHashByTxId.Add(tx.Id, block.BlockHash);
                // }

                if (block.Height != 0 && blockCommit is { })
                {
                    _chain.BlockCommit = blockCommit;
                }

                foreach (var ev in block.Evidences)
                {
                    Store.PendingEvidences.Remove(ev.Id);
                    Store.CommittedEvidences.Add(ev.Id, ev);
                }

                // Store.AppendIndex(Id, block.BlockHash);
                _nextStateRootHash = null;

                foreach (var ev in Store.PendingEvidences.ToArray())
                {
                    if (IsEvidenceExpired(ev.Value))
                    {
                        Store.PendingEvidences.Remove(ev.Key);
                        // Store.DeletePendingEvidence(ev.Id);
                    }
                }
            }
            finally
            {
                _rwlock.ExitWriteLock();
            }

            if (IsCanonical)
            {
                _logger.Information(
                    "Unstaging {TxCount} transactions from block #{BlockHeight} {BlockHash}...",
                    block.Transactions.Count(),
                    block.Height,
                    block.BlockHash);
                foreach (Transaction tx in block.Transactions)
                {
                    StagedTransactions.Remove(tx.Id);
                }

                _logger.Information(
                    "Unstaged {TxCount} transactions from block #{BlockHeight} {BlockHash}...",
                    block.Transactions.Count(),
                    block.Height,
                    block.BlockHash);
            }
            else
            {
                _logger.Information(
                    "Skipping unstaging transactions from block #{BlockHeight} {BlockHash} " +
                    "for non-canonical chain {ChainID}",
                    block.Height,
                    block.BlockHash,
                    Id);
            }

            TipChanged?.Invoke(this, (prevTip, block));
            _logger.Information(
                "Appended the block #{BlockHeight} {BlockHash}",
                block.Height,
                block.BlockHash);

            HashDigest<SHA256> nextStateRootHash =
                DetermineNextBlockStateRootHash(block, out var actionEvaluations);
            _nextStateRootHash = nextStateRootHash;

            IEnumerable<TxExecution> txExecutions = MakeTxExecutions(block, actionEvaluations);
            Store.TxExecutions.AddRange(txExecutions);

            if (render)
            {
                _renderBlock.OnNext(new RenderBlockInfo(prevTip ?? Genesis, block));
                foreach (var evaluation in actionEvaluations)
                {
                    _renderAction.OnNext(RenderActionInfo.Create(evaluation));
                }

                _renderBlockEnd.OnNext(new RenderBlockInfo(prevTip ?? Genesis, block));
            }
        }
        finally
        {
            _rwlock.ExitUpgradeableReadLock();
        }
    }

    internal void AppendStateRootHashPreceded(
        Block block,
        BlockCommit blockCommit,
        bool render,
        IReadOnlyList<CommittedActionEvaluation> actionEvaluations = null)
    {
        if (Blocks.Count == 0)
        {
            throw new ArgumentException(
                "Cannot append a block to an empty chain.");
        }
        else if (block.Height == 0)
        {
            throw new ArgumentException(
                $"Cannot append genesis block #{block.Height} {block.BlockHash} to a chain.",
                nameof(block));
        }

        _logger.Information(
            "Trying to append block #{BlockHeight} {BlockHash}...", block.Height, block.BlockHash);

        block.Header.Timestamp.ValidateTimestamp();

        _rwlock.EnterUpgradeableReadLock();
        Block prevTip = Tip;
        try
        {
            ValidateBlock(block);
            ValidateBlockCommit(block, blockCommit);

            var nonceDeltas = ValidateBlockNonces(
                block.Transactions
                    .Select(tx => tx.Signer)
                    .Distinct()
                    .ToDictionary(signer => signer, _chain.GetNonce),
                block);

            Options.BlockValidation(this, block);

            foreach (Transaction tx in block.Transactions)
            {
                Options.ValidateTransaction(this, tx);
            }

            _rwlock.EnterWriteLock();
            try
            {
                if (actionEvaluations is null)
                {
                    _logger.Information(
                        "Executing actions in block #{BlockHeight} {BlockHash}...",
                        block.Height,
                        block.BlockHash);
                    ValidateBlockPrecededStateRootHash(block, out actionEvaluations);
                    _logger.Information(
                        "Executed actions in block #{BlockHeight} {BlockHash}",
                        block.Height,
                        block.BlockHash);
                }

                // FIXME: Using evaluateActions as a proxy flag for preloading status.
                const string TimestampFormat = "yyyy-MM-ddTHH:mm:ss.ffffffZ";
                _logger
                    .ForContext("Tag", "Metric")
                    .ForContext("Subtag", "BlockAppendTimestamp")
                    .Information(
                        "Block #{BlockHeight} {BlockHash} with " +
                        "timestamp {BlockTimestamp} appended at {AppendTimestamp}",
                        block.Height,
                        block.BlockHash,
                        block.Timestamp.ToString(
                            TimestampFormat, CultureInfo.InvariantCulture),
                        DateTimeOffset.UtcNow.ToString(
                            TimestampFormat, CultureInfo.InvariantCulture));

                Blocks[block.BlockHash] = block;

                foreach (KeyValuePair<Address, long> pair in nonceDeltas)
                {
                    Nonces.Increase(pair.Key, pair.Value);
                }

                // foreach (var tx in block.Transactions)
                // {
                //     Store.BlockHashByTxId.Add(tx.Id, block.BlockHash);
                // }

                if (block.Height != 0 && blockCommit is { })
                {
                    _chain.BlockCommit = blockCommit;
                }

                foreach (var evidence in block.Evidences)
                {
                    Store.PendingEvidences.Remove(evidence.Id);
                    // if (Store.GetPendingEvidence(evidence.Id) != null)
                    // {
                    //     Store.DeletePendingEvidence(evidence.Id);
                    // }

                    // Store.PutCommittedEvidence(evidence);
                    Store.CommittedEvidences.Add(evidence.Id, evidence);
                }

                // Store.AppendIndex(Id, block.BlockHash);
                _nextStateRootHash = block.StateRootHash;
                IEnumerable<TxExecution> txExecutions = MakeTxExecutions(block, actionEvaluations);
                Store.TxExecutions.AddRange(txExecutions);

                foreach (var evidence in Store.PendingEvidences.ToArray())
                {
                    if (IsEvidenceExpired(evidence.Value))
                    {
                        Store.PendingEvidences.Remove(evidence.Key);
                    }
                }
            }
            finally
            {
                _rwlock.ExitWriteLock();
            }

            if (IsCanonical)
            {
                _logger.Information(
                    "Unstaging {TxCount} transactions from block #{BlockHeight} {BlockHash}...",
                    block.Transactions.Count(),
                    block.Height,
                    block.BlockHash);
                foreach (Transaction tx in block.Transactions)
                {
                    StagedTransactions.Remove(tx.Id);
                }

                _logger.Information(
                    "Unstaged {TxCount} transactions from block #{BlockHeight} {BlockHash}...",
                    block.Transactions.Count(),
                    block.Height,
                    block.BlockHash);
            }
            else
            {
                _logger.Information(
                    "Skipping unstaging transactions from block #{BlockHeight} {BlockHash} " +
                    "for non-canonical chain {ChainID}",
                    block.Height,
                    block.BlockHash,
                    Id);
            }

            TipChanged?.Invoke(this, (prevTip, block));
            _logger.Information(
                "Appended the block #{BlockHeight} {BlockHash}",
                block.Height,
                block.BlockHash);

            if (render)
            {
                // _logger.Information(
                //     "Invoking {RendererCount} renderers and " +
                //     "{ActionRendererCount} action renderers for #{BlockHeight} {BlockHash}",
                //     Renderers.Count,
                //     ActionRenderers.Count,
                //     block.Height,
                //     block.BlockHash);
                // foreach (IRenderer renderer in Renderers)
                // {
                //     renderer.RenderBlock(oldTip: prevTip ?? Genesis, newTip: block);
                // }

                // if (ActionRenderers.Any())
                // {
                //     RenderActions(evaluations: actionEvaluations, block: block);
                //     foreach (IActionRenderer renderer in ActionRenderers)
                //     {
                //         renderer.RenderBlockEnd(oldTip: prevTip ?? Genesis, newTip: block);
                //     }
                // }

                // _logger.Information(
                //     "Invoked {RendererCount} renderers and " +
                //     "{ActionRendererCount} action renderers for #{BlockHeight} {BlockHash}",
                //     Renderers.Count,
                //     ActionRenderers.Count,
                //     block.Height,
                //     block.BlockHash);
                _renderBlock.OnNext(new RenderBlockInfo(prevTip ?? Genesis, block));
                foreach (var evaluation in actionEvaluations)
                {
                    _renderAction.OnNext(RenderActionInfo.Create(evaluation));
                }

                _renderBlockEnd.OnNext(new RenderBlockInfo(prevTip ?? Genesis, block));
            }
        }
        finally
        {
            _rwlock.ExitUpgradeableReadLock();
        }
    }

    internal BlockHash? FindBranchpoint(BlockLocator locator)
    {
        if (Blocks.ContainsKey(locator.Hash))
        {
            _logger.Debug(
                "Found a branchpoint with locator [{LocatorHead}]: {Hash}",
                locator.Hash,
                locator.Hash);
            return locator.Hash;
        }

        _logger.Debug(
            "Failed to find a branchpoint locator [{LocatorHead}]",
            locator.Hash);
        return null;
    }

    internal ImmutableList<Transaction> ListStagedTransactions(IComparer<Transaction>? txPriority = null)
    {
        var unorderedTxs = StagedTransactions.Iterate();
        if (txPriority is { } comparer)
        {
            unorderedTxs = unorderedTxs.OrderBy(tx => tx, comparer).ToImmutableArray();
        }

        Transaction[] txs = unorderedTxs.ToArray();

        Dictionary<Address, LinkedList<Transaction>> seats = txs
            .GroupBy(tx => tx.Signer)
            .Select(g => (g.Key, new LinkedList<Transaction>(g.OrderBy(tx => tx.Nonce))))
            .ToDictionary(pair => pair.Key, pair => pair.Item2);

        return txs.Select(tx =>
        {
            LinkedList<Transaction> seat = seats[tx.Signer];
            Transaction first = seat.First.Value;
            seat.RemoveFirst();
            return first;
        }).ToImmutableList();
    }

    internal void CleanupBlockCommitStore(long limit)
    {
        // FIXME: This still isn't enough to prevent the canonical chain
        // removing cached block commits that are needed by other non-canonical chains.
        if (!IsCanonical)
        {
            throw new InvalidOperationException(
                $"Cannot perform {nameof(CleanupBlockCommitStore)}() from a " +
                "non canonical chain.");
        }

        List<BlockHash> hashes = Store.BlockCommits.Keys.ToList();

        _logger.Debug("Removing old BlockCommits with heights lower than {Limit}...", limit);
        foreach (var hash in hashes)
        {
            if (Store.BlockCommits.TryGetValue(hash, out var commit) && commit.Height < limit)
            {
                Store.BlockCommits.Remove(hash);
            }
        }
    }

    internal HashDigest<SHA256>? GetNextStateRootHash() => _nextStateRootHash;

    internal HashDigest<SHA256>? GetNextStateRootHash(int index) =>
        GetNextStateRootHash(Blocks[index]);

    internal HashDigest<SHA256>? GetNextStateRootHash(BlockHash blockHash) => GetNextStateRootHash(Blocks[blockHash]);

    internal ImmutableSortedSet<Validator> GetValidatorSet(int index)
    {
        if (index == 0)
        {
            IWorldContext worldContext = new WorldStateContext(GetNextWorld());
            return (ImmutableSortedSet<Validator>)worldContext[ReservedAddresses.ValidatorSetAddress][ReservedAddresses.ValidatorSetAddress]; ;
        }

        if (GetBlockCommit(index) is { } commit)
        {
            return [.. commit.Votes.Select(CreateValidator)];
        }

        throw new ArgumentException("Cannot find a validator set for the given index.");

        static Validator CreateValidator(Vote vote)
            => Validator.Create(vote.Validator, vote.ValidatorPower);
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
