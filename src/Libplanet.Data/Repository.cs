using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Libplanet.Types;
using Libplanet.Types.Progresses;

namespace Libplanet.Data;

public class Repository
{
    private const string GenesisHeightKey = "genesisHeight";
    private const string HeightKey = "height";
    private const string StateRootHashKey = "stateRootHash";
    private const string IdKey = "id";

    private readonly ImmutableMetadataIndex _immutableMetadata;
    private readonly MetadataIndex _metadata;
    private int _genesisHeight = -1;
    private int _height = -1;
    private HashDigest<SHA256> _stateRootHash;

    public Repository()
        : this(new MemoryDatabase())
    {
    }

    public Repository(IDatabase database)
    {
        Database = database;
        _immutableMetadata = new ImmutableMetadataIndex(database);
        _metadata = new MetadataIndex(database);
        BlockDigests = new BlockDigestIndex(database);
        BlockCommits = new BlockCommitIndex(database);
        StateRootHashes = new StateRootHashIndex(database);
        PendingTransactions = new PendingTransactionIndex(database);
        CommittedTransactions = new CommittedTransactionIndex(database);
        PendingEvidences = new PendingEvidenceIndex(database);
        CommittedEvidences = new CommittedEvidenceIndex(database);
        TxExecutions = new TxExecutionIndex(database);
        BlockExecutions = new BlockExecutionIndex(database);
        BlockHashes = new BlockHashIndex(database);
        Nonces = new NonceIndex(database);
        States = new StateIndex(database);
        if (_metadata.TryGetValue(GenesisHeightKey, out var genesisHeight))
        {
            _genesisHeight = int.Parse(genesisHeight);
        }

        if (_metadata.TryGetValue(HeightKey, out var height))
        {
            _height = int.Parse(height);
        }

        _stateRootHash = _metadata.TryGetValue(StateRootHashKey, out var s1) ? HashDigest<SHA256>.Parse(s1) : default;

        if (_immutableMetadata.TryGetValue(IdKey, out var id))
        {
            Id = Guid.Parse(id);
        }
        else
        {
            Id = Guid.NewGuid();
            _immutableMetadata[IdKey] = Id.ToString();
        }

        BlockHashes.Height = _height;
    }

    public Guid Id { get; private set; }

    public PendingEvidenceIndex PendingEvidences { get; }

    public CommittedEvidenceIndex CommittedEvidences { get; }

    public PendingTransactionIndex PendingTransactions { get; }

    public CommittedTransactionIndex CommittedTransactions { get; }

    public BlockCommitIndex BlockCommits { get; }

    public BlockDigestIndex BlockDigests { get; }

    public StateRootHashIndex StateRootHashes { get; }

    public TxExecutionIndex TxExecutions { get; }

    public BlockExecutionIndex BlockExecutions { get; }

    public BlockHashIndex BlockHashes { get; }

    public NonceIndex Nonces { get; }

    public StateIndex States { get; }

    public int GenesisHeight
    {
        get => _genesisHeight;
        set
        {
            if (value < -1)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Genesis height cannot be less than -1.");
            }

            _genesisHeight = value;
            _metadata[IdKey] = Id.ToString();

            if (value != -1)
            {
                _metadata[GenesisHeightKey] = _genesisHeight.ToString();
            }
            else
            {
                _metadata.Remove(GenesisHeightKey);
            }
        }
    }

    public int Height
    {
        get => _height;
        set
        {
            if (value < -1)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Height cannot be less than -1.");
            }

            _height = value;
            BlockHashes.Height = value;
            if (value != -1)
            {
                _metadata[HeightKey] = _height.ToString();
            }
            else
            {
                _metadata.Remove(HeightKey);
            }
        }
    }

    public HashDigest<SHA256> StateRootHash
    {
        get => _stateRootHash;
        set
        {
            _stateRootHash = value;
            if (value != default)
            {
                _metadata[StateRootHashKey] = _stateRootHash.ToString();
            }
            else
            {
                _metadata.Remove(StateRootHashKey);
            }
        }
    }

    public BlockHash GenesisBlockHash => _genesisHeight == -1 ? default : BlockHashes[_genesisHeight];

    public BlockHash BlockHash => _height == -1 ? default : BlockHashes[_height];

    public BlockCommit BlockCommit => BlockCommits.GetValueOrDefault(BlockHash, default);

    public DateTimeOffset Timestamp => _height == -1 ? default : BlockDigests[BlockHash].Timestamp;

    public int BlockVersion { get; set; } = BlockHeader.CurrentProtocolVersion;

    public bool IsEmpty => _genesisHeight == -1
        && _height == -1
        && _stateRootHash == default
        && _metadata.IsEmpty
        && BlockDigests.IsEmpty
        && BlockCommits.IsEmpty
        && StateRootHashes.IsEmpty
        && PendingTransactions.IsEmpty
        && CommittedTransactions.IsEmpty
        && PendingEvidences.IsEmpty
        && CommittedEvidences.IsEmpty
        && TxExecutions.IsEmpty
        && BlockExecutions.IsEmpty
        && BlockHashes.IsEmpty
        && Nonces.IsEmpty
        && States.IsEmpty;

    protected IDatabase Database { get; }

    public void Append(Block block, BlockCommit blockCommit)
    {
        if (_genesisHeight == -1)
        {
            if (blockCommit != default)
            {
                throw new ArgumentException(
                    "Genesis block cannot have a block commit.",
                    nameof(blockCommit));
            }

            if (block.PreviousStateRootHash != default && !States.ContainsKey(block.PreviousStateRootHash))
            {
                throw new ArgumentException(
                    $"Cannot find previous state root hash: {block.PreviousStateRootHash}.",
                    nameof(block));
            }
        }

        if (blockCommit != default)
        {
            if (blockCommit.BlockHash != block.BlockHash)
            {
                throw new ArgumentException(
                    "Block commit's block hash does not match the block's hash.",
                    nameof(blockCommit));
            }

            if (blockCommit.Height != block.Height)
            {
                throw new ArgumentException(
                    "Block commit's height does not match the block's height.",
                    nameof(blockCommit));
            }
        }

        if (_height >= 0 && _height + 1 != block.Height)
        {
            throw new ArgumentException(
                "Block height does not match the current height + 1.",
                nameof(block));
        }

        if (_genesisHeight == -1)
        {
            GenesisHeight = block.Height;
        }

        Nonces.Validate(block);

        BlockDigests.Add(block);
        if (blockCommit != default)
        {
            BlockCommits.Add(blockCommit);
        }

        BlockHashes.Add(block);
        Nonces.Increase(block);
        PendingTransactions.RemoveRange(block.Transactions);
        CommittedTransactions.AddRange(block.Transactions);
        PendingEvidences.RemoveRange(block.Evidences);
        CommittedEvidences.AddRange(block.Evidences);
        Height = block.Height;
    }

    public Block GetBlock(BlockHash blockHash)
    {
        var blockDigest = BlockDigests[blockHash];
        return blockDigest.ToBlock(item => CommittedTransactions[item], item => CommittedEvidences[item]);
    }

    public Block GetBlock(int height)
    {
        var blockHash = BlockHashes[height];
        return GetBlock(blockHash);
    }

    public bool TryGetBlock(BlockHash blockHash, [MaybeNullWhen(false)] out Block block)
    {
        if (BlockDigests.TryGetValue(blockHash, out var blockDigest))
        {
            block = blockDigest.ToBlock(item => CommittedTransactions[item], item => CommittedEvidences[item]);
            return true;
        }

        block = null;
        return false;
    }

    public bool TryGetBlock(int height, [MaybeNullWhen(false)] out Block block)
    {
        if (BlockHashes.TryGetValue(height, out var blockHash))
        {
            return TryGetBlock(blockHash, out block);
        }

        block = null;
        return false;
    }

    public Block? GetBlockOrDefault(BlockHash blockHash)
    {
        if (BlockDigests.TryGetValue(blockHash, out var blockDigest))
        {
            return blockDigest.ToBlock(item => CommittedTransactions[item], item => CommittedEvidences[item]);
        }

        return null;
    }

    public Block? GetBlockOrDefault(int height)
    {
        if (BlockHashes.TryGetValue(height, out var blockHash))
        {
            return GetBlockOrDefault(blockHash);
        }

        return null;
    }

    public long GetNonce(Address address)
    {
        if (Nonces.TryGetValue(address, out var nonce))
        {
            return nonce;
        }

        return 0;
    }

    public void Clear()
    {
        var tables = Database.Where(item => item.Key != ImmutableMetadataIndex.Name).ToArray();
        foreach (var (_, table) in tables)
        {
            table.Clear();
        }
    }

    public async Task CopyToAsync(
        Repository destination, CancellationToken cancellationToken, IProgress<ProgressInfo> progress)
    {
        if (!destination.IsEmpty)
        {
            throw new ArgumentException("Destination repository is not empty.", nameof(destination));
        }

        var tables = Database.Where(item => item.Key != ImmutableMetadataIndex.Name).ToArray();
        var stepProgress = new StepProgress(tables.Length + 1, progress);
        foreach (var (name, sourceTable) in tables)
        {
            var destTable = destination.Database.GetOrAdd(name);
            if (sourceTable.Count > 0)
            {
                await CopyTableAsync(sourceTable, destTable, stepProgress, cancellationToken);
            }
            else
            {
                stepProgress.Next($"Skipping empty table '{name}'...");
            }
        }

        stepProgress.Next("Copying database state...");
        destination._genesisHeight = _genesisHeight;
        destination._height = _height;
        destination._stateRootHash = _stateRootHash;

        stepProgress.Complete("Completed.");
    }

    private static async Task CopyTableAsync(
        ITable source, ITable destination, StepProgress progress, CancellationToken cancellationToken)
    {
        var stepProgress = progress.BeginSubProgress(source.Count);
        foreach (var (key, value) in source)
        {
            stepProgress.Next($"Copying table '{source.Name}' key '{key}'...");
            cancellationToken.ThrowIfCancellationRequested();
            destination[key] = value;
            await Task.Yield();
        }

        stepProgress.Complete($"Copied table '{source.Name}' with {source.Count} entries.");
    }
}

public class Repository<TDatabase>(TDatabase database) : Repository(database)
    where TDatabase : IDatabase
{
    public new TDatabase Database => (TDatabase)base.Database;
}
