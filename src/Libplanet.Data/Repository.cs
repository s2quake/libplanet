using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Libplanet.Types;

namespace Libplanet.Data;

public class Repository
{
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
        _metadata = new MetadataIndex(Database);
        BlockDigests = new BlockDigestIndex(Database);
        BlockCommits = new BlockCommitIndex(Database);
        StateRootHashes = new StateRootHashIndex(Database);
        PendingTransactions = new PendingTransactionIndex(Database);
        CommittedTransactions = new CommittedTransactionIndex(Database);
        PendingEvidences = new PendingEvidenceIndex(Database);
        CommittedEvidences = new CommittedEvidenceIndex(Database);
        TxExecutions = new TxExecutionIndex(Database);
        BlockExecutions = new BlockExecutionIndex(Database);
        BlockHashes = new BlockHashIndex(database);
        Nonces = new NonceIndex(database);
        States = new StateIndex(Database);
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

    public BlockHash GenesisBlockHash => _genesisHeight == -1 ? default : BlockHashes[_genesisHeight];

    public BlockHash BlockHash => _height == -1 ? default : BlockHashes[_height];

    public BlockCommit BlockCommit => BlockCommits.GetValueOrDefault(BlockHash, BlockCommit.Empty);

    protected IDatabase Database { get; }

    public void Append(Block block, BlockCommit blockCommit)
    {
        if (blockCommit != BlockCommit.Empty)
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

        BlockDigests.Add(block);
        if (blockCommit != BlockCommit.Empty)
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
}

public class Repository<TDatabase>(TDatabase database) : Repository(database)
    where TDatabase : IDatabase
{
    public new TDatabase Database => (TDatabase)base.Database;
}
