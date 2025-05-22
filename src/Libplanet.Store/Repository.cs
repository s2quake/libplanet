using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Libplanet.Types;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;

namespace Libplanet.Store;

public sealed class Repository : IDisposable
{
    private readonly IDatabase _database;
    private readonly MetadataStore _metadata;
    private int _genesisHeight = -1;
    private int _height = -1;
    private HashDigest<SHA256> _stateRootHash;

    private bool _disposed;

    public Repository()
        : this(new MemoryDatabase())
    {
    }

    public Repository(IDatabase database)
    {
        _database = database;
        _metadata = new MetadataStore(_database);
        BlockDigests = new BlockDigestStore(_database);
        BlockCommits = new BlockCommitStore(_database);
        StateRootHashStore = new StateRootHashStore(_database);
        PendingTransactions = new PendingTransactionStore(_database);
        CommittedTransactions = new CommittedTransactionStore(_database);
        PendingEvidences = new PendingEvidenceStore(_database);
        CommittedEvidences = new CommittedEvidenceStore(_database);
        TxExecutions = new TxExecutionStore(_database);
        BlockExecutions = new BlockExecutionStore(_database);
        BlockHashes = new BlockHashStore(database);
        Nonces = new NonceStore(database);
        StateStore = new TrieStateStore(_database);
        if (_metadata.TryGetValue("genesisHeight", out var genesisHeight))
        {
            _genesisHeight = int.Parse(genesisHeight);
        }

        if (_metadata.TryGetValue("height", out var height))
        {
            _height = int.Parse(height);
        }

        _stateRootHash = _metadata.TryGetValue("stateRootHash", out var s1) ? HashDigest<SHA256>.Parse(s1) : default;

        if (_metadata.TryGetValue("id", out var id))
        {
            Id = Guid.Parse(id);
        }
        else
        {
            Id = Guid.NewGuid();
            _metadata["id"] = Id.ToString();
        }

        BlockHashes.GenesisHeight = _genesisHeight;
        BlockHashes.Height = _height;
    }

    public Guid Id { get; }

    public PendingEvidenceStore PendingEvidences { get; }

    public CommittedEvidenceStore CommittedEvidences { get; }

    public PendingTransactionStore PendingTransactions { get; }

    public CommittedTransactionStore CommittedTransactions { get; }

    public BlockCommitStore BlockCommits { get; }

    public BlockDigestStore BlockDigests { get; }

    public StateRootHashStore StateRootHashStore { get; }

    public TxExecutionStore TxExecutions { get; }

    public BlockExecutionStore BlockExecutions { get; }

    public BlockHashStore BlockHashes { get; }

    public NonceStore Nonces { get; }

    public TrieStateStore StateStore { get; }

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
            BlockHashes.GenesisHeight = value;
            if (value != -1)
            {
                _metadata["genesisHeight"] = _genesisHeight.ToString();
            }
            else
            {
                _metadata.Remove("genesisHeight");
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
                _metadata["height"] = _height.ToString();
            }
            else
            {
                _metadata.Remove("height");
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
                _metadata["stateRootHash"] = _stateRootHash.ToString();
            }
            else
            {
                _metadata.Remove("stateRootHash");
            }
        }
    }

    public BlockHash GenesisBlockHash
    {
        get
        {
            if (_genesisHeight == -1)
            {
                throw new InvalidOperationException("Genesis block hash is not set.");
            }

            return BlockHashes[_genesisHeight];
        }
    }

    public BlockHash BlockHash
    {
        get
        {
            if (_height == -1)
            {
                throw new InvalidOperationException("Block hash is not set.");
            }

            return BlockHashes[_height];
        }
    }

    public BlockCommit BlockCommit => BlockCommits[BlockHash];

    public void Append(Block block, BlockCommit blockCommit)
    {
        BlockDigests.Add(block);
        BlockCommits.Add(block.BlockHash, blockCommit);
        BlockHashes.Add(block);
        Nonces.Increase(block);
        PendingTransactions.RemoveRange(block.Transactions);
        CommittedTransactions.AddRange(block.Transactions);
        PendingEvidences.RemoveRange(block.Evidences);
        CommittedEvidences.AddRange(block.Evidences);
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
            block = blockDigest.ToBlock(item => PendingTransactions[item], item => CommittedEvidences[item]);
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
            return blockDigest.ToBlock(item => PendingTransactions[item], item => CommittedEvidences[item]);
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

    public void Dispose()
    {
        if (!_disposed)
        {
            _database.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
