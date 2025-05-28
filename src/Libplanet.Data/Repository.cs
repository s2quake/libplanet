using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Libplanet.Types;
using Libplanet.Types;
using Libplanet.Types;

namespace Libplanet.Data;

public sealed class Repository : IDisposable
{
    private readonly IDatabase _database;
    private readonly MetadataIndex _metadata;
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
        _metadata = new MetadataIndex(_database);
        BlockDigests = new BlockDigestIndex(_database);
        BlockCommits = new BlockCommitIndex(_database);
        StateRootHashes = new StateRootHashIndex(_database);
        PendingTransactions = new PendingTransactionIndex(_database);
        CommittedTransactions = new CommittedTransactionIndex(_database);
        PendingEvidences = new PendingEvidenceIndex(_database);
        CommittedEvidences = new CommittedEvidenceIndex(_database);
        TxExecutions = new TxExecutionIndex(_database);
        BlockExecutions = new BlockExecutionIndex(_database);
        BlockHashes = new BlockHashIndex(database);
        Nonces = new NonceIndex(database);
        States = new StateIndex(_database);
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
