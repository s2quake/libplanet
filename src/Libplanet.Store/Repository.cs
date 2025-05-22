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
    private int _genesisHeight;
    private int _height;
    private HashDigest<SHA256> _stateRootHash;
    private BlockCommit _blockCommit = BlockCommit.Empty;

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
        BlockHashes = new BlockHashStore(database)
        {
            GenesisHeight = GenesisHeight,
            Height = Height,
        };
        Nonces = new NonceStore(database);
        // Chains = new ChainStore(_database);
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

        // if (_metadata.TryGetValue("chainId", out var chainId))
        // {
        //     _chain = Chains[Guid.Parse(chainId)];
        // }
        // else
        // {
        //     _chain = Chains.AddNew(Guid.NewGuid());
        //     _metadata["chainId"] = _chain.Id.ToString();
        // }
    }

    public Guid Id { get; }

    // public Repository(Block genesisBlock)
    //     : this(genesisBlock, new MemoryDatabase())
    // {
    // }

    // public Repository(Block genesisBlock, IDatabase database)
    //     : this(database)
    // {
    //     AddNewChain(genesisBlock);
    // }

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

    // public ChainStore Chains { get; }

    public int GenesisHeight
    {
        get => _genesisHeight;
        set
        {
            _genesisHeight = value;
            _metadata["genesisHeight"] = _genesisHeight.ToString();
        }
    }

    public int Height
    {
        get => _height;
        set
        {
            _height = value;
            _metadata["height"] = _height.ToString();
        }
    }

    public HashDigest<SHA256> StateRootHash
    {
        get => _stateRootHash;
        set
        {
            _stateRootHash = value;
            _metadata["stateRootHash"] = _stateRootHash.ToString();
        }
    }

    public BlockHash GenesisBlockHash => BlockHashes[_genesisHeight];

    public BlockHash BlockHash => BlockHashes[_height];

    public BlockCommit BlockCommit => BlockCommits[BlockHash];

    // public HashDigest<SHA256> StateRootHash { get; set; }

    // public Guid ChainId
    // {
    //     get => _metadata.TryGetValue("chainId", out var chainId) ? Guid.Parse(chainId) : Guid.Empty;
    //     set
    //     {
    //         if (value == Guid.Empty)
    //         {
    //             _chain = null;
    //             _metadata.Remove("chainId");
    //         }
    //         else
    //         {
    //             _chain = Chains[value];
    //             _metadata["chainId"] = value.ToString();
    //         }
    //     }
    // }

    // public Chain Chain => _chain ?? throw new InvalidOperationException(
    //     "ChainId is not set. Please set ChainId before accessing the Chain property.");

    public TrieStateStore StateStore { get; }

    // public Chain AddNewChain(Block genesisBlock)
    // {
    //     _chain = Chains.AddNew(Guid.NewGuid());
    //     _metadata["chainId"] = _chain.Id.ToString();
    //     Append(genesisBlock, BlockCommit.Empty);
    //     _chain.Append(genesisBlock, BlockCommit.Empty);
    //     return _chain;
    // }

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
