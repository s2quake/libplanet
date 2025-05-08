using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reactive.Subjects;
using System.Security.Cryptography;
using System.Threading;
using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.Action.State;
using Libplanet.Store;
using Libplanet.Types;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Libplanet.Types.Crypto;
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

    private readonly BlockSet _blocks;
    private Block _genesis;

    private HashDigest<SHA256>? _nextStateRootHash;

    public BlockChain(Block genesisBlock, BlockChainOptions options)
        : this(genesisBlock, options.Store.GetCanonicalChainId(), options)
    {
    }

    private BlockChain(Block genesisBlock, Guid id, BlockChainOptions options)
    {
        // if (store.CountIndex(id) == 0)
        // {
        //     throw new ArgumentException(
        //         $"Given store does not contain chain id {id}.", nameof(store));
        // }

        Id = id;
        Options = options;
        StagedTransactions = new StagedTransactionCollection(options.Store, id);
        Store = options.Store;
        StateStore = new TrieStateStore(options.KeyValueStore);

        _blockChainStates = new BlockChainStates(options.Store, StateStore);

        _blocks = new BlockSet(options.Store);
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
                $"The genesis block that the given {nameof(IStore)} contains does not match " +
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
        UpdateTxExecutions(txExecutions);
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

    public Block Tip => this[-1];

    public Block Genesis => _genesis ??= this[0];

    public Guid Id { get; private set; }

    public IEnumerable<BlockHash> BlockHashes => IterateBlockHashes();

    public long Count => Store.CountIndex(Id);

    internal IStore Store { get; }

    internal TrieStateStore StateStore { get; }

    internal ActionEvaluator ActionEvaluator { get; }

    internal bool IsCanonical => Store.GetCanonicalChainId() is Guid guid && Id == guid;

    public Block this[long height]
    {
        get
        {
            _rwlock.EnterReadLock();
            try
            {
                BlockHash? blockHash = Store.GetBlockHash(Id, height);
                return blockHash is { } bh
                    ? _blocks[bh]
                    : throw new ArgumentOutOfRangeException();
            }
            finally
            {
                _rwlock.ExitReadLock();
            }
        }
    }

    public Block this[in BlockHash blockHash]
    {
        get
        {
            if (!ContainsBlock(blockHash))
            {
                throw new KeyNotFoundException(
                    $"The given hash[{blockHash}] was not found in this chain.");
            }

            _rwlock.EnterReadLock();
            try
            {
                return _blocks[blockHash];
            }
            finally
            {
                _rwlock.ExitReadLock();
            }
        }
    }

    public static BlockChain Create(Block genesisBlock, BlockChainOptions options)
    {
        if (options.Store.GetCanonicalChainId() is { } canonId && canonId != Guid.Empty)
        {
            throw new ArgumentException(
                $"Given {nameof(options.Store)} already has its canonical chain id set: {canonId}",
                nameof(options.Store));
        }

        var id = Guid.NewGuid();

        // if (genesisBlock.ProtocolVersion < BlockHeader.SlothProtocolVersion)
        // {
        //     var preEval = new RawBlock
        //     {
        //         Metadata = (BlockHeader)genesisBlock.Header,
        //         RawHash = genesisBlock.Header.RawHash,
        //         Content = new BlockContent
        //         {
        //             // Metadata = genesisBlock.RawBlock.Header.Metadata,
        //             Transactions = genesisBlock.Transactions,
        //             Evidence = genesisBlock.Evidence,
        //         },
        //         // Header = genesisBlock.RawBlock.Header,
        //     };
        //     var computedStateRootHash =
        //         actionEvaluator.Evaluate(preEval, default)[^1].OutputState;
        //     if (!genesisBlock.StateRootHash.Equals(computedStateRootHash))
        //     {
        //         throw new InvalidOperationException(
        //             $"Given block #{genesisBlock.Index} {genesisBlock.Hash} has " +
        //             $"a state root hash {genesisBlock.StateRootHash} that is different " +
        //             $"from the calculated state root hash {computedStateRootHash}");
        //     }
        // }

        ValidateGenesis(genesisBlock);
        var nonceDeltas = ValidateGenesisNonces(genesisBlock);

        options.Store.PutBlock(genesisBlock);
        options.Store.AppendIndex(id, genesisBlock.Height, genesisBlock.BlockHash);

        foreach (var tx in genesisBlock.Transactions)
        {
            options.Store.PutTxIdBlockHashIndex(tx.Id, genesisBlock.BlockHash);
        }

        foreach (KeyValuePair<Address, long> pair in nonceDeltas)
        {
            options.Store.IncreaseTxNonce(id, pair.Key, pair.Value);
        }

        options.Store.SetCanonicalChainId(id);

        return new BlockChain(genesisBlock, id, options);
    }

    public bool ContainsBlock(BlockHash blockHash)
    {
        _rwlock.EnterReadLock();
        try
        {
            return
                _blocks.ContainsKey(blockHash) &&
                Store.GetBlockHeight(blockHash) is { } branchPointIndex &&
                branchPointIndex <= Tip.Height &&
                Store.GetBlockHash(Id, branchPointIndex).Equals(blockHash);
        }
        finally
        {
            _rwlock.ExitReadLock();
        }
    }

    public Transaction GetTransaction(TxId txId)
    {
        if (StagedTransactions.Get(txId) is { } tx)
        {
            return tx;
        }

        _rwlock.EnterReadLock();
        try
        {
            if (Store.GetTransaction(txId) is { } transaction)
            {
                return transaction;
            }

            throw new KeyNotFoundException($"No such transaction: {txId}");
        }
        finally
        {
            _rwlock.ExitReadLock();
        }
    }

    public TxExecution GetTxExecution(BlockHash blockHash, TxId txid) => Store.GetTxExecution(blockHash, txid);

    public void Append(
        Block block,
        BlockCommit blockCommit,
        bool validate = true)
    {
        Append(block, blockCommit, render: true, validate: validate);
    }

    public bool StageTransaction(Transaction transaction)
    {
        if (!transaction.GenesisHash.Equals(Genesis.BlockHash))
        {
            var msg = "GenesisHash of the transaction is not compatible " +
                      "with the BlockChain.Genesis.Hash.";
            throw new InvalidOperationException(
                msg);
        }

        return StagedTransactions.Stage(transaction);
    }

    public bool UnstageTransaction(Transaction transaction) => StagedTransactions.Unstage(transaction.Id);

    public long GetNextTxNonce(Address address) => StagedTransactions.GetNextTxNonce(address);

    public Transaction MakeTransaction(
        PrivateKey privateKey,
        IEnumerable<IAction> actions,
        FungibleAssetValue? maxGasPrice = null,
        long gasLimit = 0L,
        DateTimeOffset? timestamp = null)
    {
        timestamp = timestamp ?? DateTimeOffset.UtcNow;
        lock (_txLock)
        {
            // FIXME: Exception should be documented when the genesis block does not exist.
            Transaction tx = Transaction.Create(
                GetNextTxNonce(privateKey.Address),
                privateKey,
                Genesis.BlockHash,
                actions.ToBytecodes(),
                maxGasPrice,
                gasLimit,
                timestamp);
            StageTransaction(tx);
            return tx;
        }
    }

    public IImmutableSet<TxId> GetStagedTransactionIds()
    {
        // FIXME: How about turning this method to the StagedTransactions property?
        return StagedTransactions.Iterate().Select(tx => tx.Id).ToImmutableHashSet();
    }

    public IReadOnlyList<BlockHash> FindNextHashes(
        BlockLocator locator,
        int count = 500)
    {
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        if (!(FindBranchpoint(locator) is { } branchpoint))
        {
            return Array.Empty<BlockHash>();
        }

        if (!(Store.GetBlockHeight(branchpoint) is { } branchpointIndex))
        {
            return Array.Empty<BlockHash>();
        }

        var result = new List<BlockHash>();
        foreach (BlockHash hash in Store.IterateIndexes(Id, (int)branchpointIndex, count))
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
                Store.ListChainIds().Count(),
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

    public BlockCommit GetBlockCommit(long index)
    {
        Block block = this[index];

        // if (block.ProtocolVersion < BlockHeader.PBFTProtocolVersion)
        // {
        //     return null;
        // }

        return index == Tip.Height
            ? Store.GetChainBlockCommit(Id)
            : this[index + 1].LastCommit;
    }

    public BlockCommit GetBlockCommit(BlockHash blockHash) => GetBlockCommit(this[blockHash].Height);

    internal void Append(
        Block block,
        BlockCommit blockCommit,
        bool render,
        bool validate = true)
    {
        if (Count == 0)
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
                    .ToDictionary(signer => signer, signer => Store.GetTxNonce(Id, signer)),
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

                _blocks[block.BlockHash] = block;

                foreach (KeyValuePair<Address, long> pair in nonceDeltas)
                {
                    Store.IncreaseTxNonce(Id, pair.Key, pair.Value);
                }

                foreach (var tx in block.Transactions)
                {
                    Store.PutTxIdBlockHashIndex(tx.Id, block.BlockHash);
                }

                if (block.Height != 0 && blockCommit is { })
                {
                    Store.PutChainBlockCommit(Id, blockCommit);
                }

                foreach (var ev in block.Evidences)
                {
                    if (Store.GetPendingEvidence(ev.Id) != null)
                    {
                        Store.DeletePendingEvidence(ev.Id);
                    }

                    Store.PutCommittedEvidence(ev);
                }

                Store.AppendIndex(Id, block.Height, block.BlockHash);
                _nextStateRootHash = null;

                foreach (var ev in GetPendingEvidence().ToArray())
                {
                    if (IsEvidenceExpired(ev))
                    {
                        Store.DeletePendingEvidence(ev.Id);
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
                    UnstageTransaction(tx);
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

            IEnumerable<TxExecution> txExecutions =
                MakeTxExecutions(block, actionEvaluations);
            UpdateTxExecutions(txExecutions);

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
        if (Count == 0)
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
                    .ToDictionary(signer => signer, signer => Store.GetTxNonce(Id, signer)),
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

                _blocks[block.BlockHash] = block;

                foreach (KeyValuePair<Address, long> pair in nonceDeltas)
                {
                    Store.IncreaseTxNonce(Id, pair.Key, pair.Value);
                }

                foreach (var tx in block.Transactions)
                {
                    Store.PutTxIdBlockHashIndex(tx.Id, block.BlockHash);
                }

                if (block.Height != 0 && blockCommit is { })
                {
                    Store.PutChainBlockCommit(Id, blockCommit);
                }

                foreach (var evidence in block.Evidences)
                {
                    if (Store.GetPendingEvidence(evidence.Id) != null)
                    {
                        Store.DeletePendingEvidence(evidence.Id);
                    }

                    Store.PutCommittedEvidence(evidence);
                }

                Store.AppendIndex(Id, block.Height, block.BlockHash);
                _nextStateRootHash = block.StateRootHash;
                IEnumerable<TxExecution> txExecutions =
                    MakeTxExecutions(block, actionEvaluations);
                UpdateTxExecutions(txExecutions);

                foreach (var evidence in GetPendingEvidence().ToArray())
                {
                    if (IsEvidenceExpired(evidence))
                    {
                        Store.DeletePendingEvidence(evidence.Id);
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
                    UnstageTransaction(tx);
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
        if (ContainsBlock(locator.Hash))
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

    internal IEnumerable<Block> IterateBlocks(int offset = 0, int? limit = null)
    {
        _rwlock.EnterUpgradeableReadLock();

        try
        {
            foreach (BlockHash hash in IterateBlockHashes(offset, limit))
            {
                yield return _blocks[hash];
            }
        }
        finally
        {
            _rwlock.ExitUpgradeableReadLock();
        }
    }

    internal IEnumerable<BlockHash> IterateBlockHashes(int offset = 0, int? limit = null)
    {
        _rwlock.EnterUpgradeableReadLock();

        try
        {
            IEnumerable<BlockHash> indices = Store.IterateIndexes(Id, offset, limit);

            // NOTE: The reason why this does not simply return indices, but iterates over
            // indices and yields hashes step by step instead, is that we need to ensure
            // the read lock held until the whole iteration completes.
            foreach (BlockHash hash in indices)
            {
                yield return hash;
            }
        }
        finally
        {
            _rwlock.ExitUpgradeableReadLock();
        }
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

        List<BlockHash> hashes = Store.GetBlockCommitHashes().ToList();

        _logger.Debug("Removing old BlockCommits with heights lower than {Limit}...", limit);
        foreach (var hash in hashes)
        {
            if (Store.GetBlockCommit(hash) is { } commit && commit.Height < limit)
            {
                Store.DeleteBlockCommit(hash);
            }
        }
    }

    internal HashDigest<SHA256>? GetNextStateRootHash() => _nextStateRootHash;

    internal HashDigest<SHA256>? GetNextStateRootHash(long index) =>
        GetNextStateRootHash(this[index]);

    internal HashDigest<SHA256>? GetNextStateRootHash(BlockHash blockHash) =>
        GetNextStateRootHash(this[blockHash]);

    internal ImmutableSortedSet<Validator> GetValidatorSet(long index)
    {
        if (index == 0)
        {
            var worldContext = new WorldStateContext(GetNextWorld());
            return (ImmutableSortedSet<Validator>)worldContext[ReservedAddresses.ValidatorSetAddress][ReservedAddresses.ValidatorSetAddress]; ;
        }

        if (GetBlockCommit(index) is { } commit)
        {
            return [.. commit.Votes.Select(CreateValidator)];
        }

        throw new ArgumentException("Cannot find a validator set for the given index.");

        static Validator CreateValidator(Vote vote)
            => Validator.Create(vote.ValidatorPublicKey, vote.ValidatorPower);
    }

    private HashDigest<SHA256>? GetNextStateRootHash(Block block)
    {
        if (block.Height < Tip.Height)
        {
            return this[block.Height + 1].StateRootHash;
        }
        else
        {
            return GetNextStateRootHash();
        }
    }
}
