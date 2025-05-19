using System.Diagnostics.CodeAnalysis;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;

namespace Libplanet.Store;

public sealed class Repository : IDisposable
{
    private readonly IDatabase _database;
    private readonly MetadataStore _metadata;
    private Chain? _chain;

    private bool _disposed;

    public Repository()
        : this(new MemoryDatabase())
    {
    }

    public Repository(IDatabase database)
    {
        _database = database;
        BlockDigests = new BlockDigestStore(_database);
        TxExecutions = new TxExecutionStore(_database);
        BlockCommits = new BlockCommitStore(_database);
        PendingTransactions = new PendingTransactionStore(_database);
        CommittedTransactions = new CommittedTransactionStore(_database);
        PendingEvidences = new PendingEvidenceStore(_database);
        CommittedEvidences = new CommittedEvidenceStore(_database);
        Chains = new ChainStore(_database);
        _metadata = new MetadataStore(_database);
        StateStore = new TrieStateStore(_database);
        if (_metadata.TryGetValue("chainId", out var chainId))
        {
            _chain = Chains[Guid.Parse(chainId)];
        }
    }

    public PendingEvidenceStore PendingEvidences { get; }

    public CommittedEvidenceStore CommittedEvidences { get; }

    public PendingTransactionStore PendingTransactions { get; }

    public CommittedTransactionStore CommittedTransactions { get; }

    public BlockCommitStore BlockCommits { get; }

    public BlockDigestStore BlockDigests { get; }

    public TxExecutionStore TxExecutions { get; }

    public ChainStore Chains { get; }

    public Guid ChainId
    {
        get => _metadata.TryGetValue("chainId", out var chainId) ? Guid.Parse(chainId) : Guid.Empty;
        set
        {
            if (value == Guid.Empty)
            {
                _chain = null;
                _metadata.Remove("chainId");
            }
            else
            {
                _chain = Chains[value];
                _metadata["chainId"] = value.ToString();
            }
        }
    }

    public Chain Chain => _chain ?? throw new InvalidOperationException(
        "ChainId is not set. Please set ChainId before accessing the Chain property.");

    public TrieStateStore StateStore { get; }

    public void AddBlock(Block block)
    {
        BlockDigests.Add(block);
        PendingTransactions.AddRange(block.Transactions);
        PendingEvidences.RemoveRange(block.Evidences);
        CommittedEvidences.AddRange(block.Evidences);
    }

    public Block GetBlock(BlockHash blockHash)
    {
        var blockDigest = BlockDigests[blockHash];
        return blockDigest.ToBlock(item => PendingTransactions[item], item => CommittedEvidences[item]);
    }

    public Block GetBlock(int height) => GetBlock(ChainId, height);

    public Block GetBlock(Guid chainId, int height)
    {
        var chain = Chains[chainId];
        var blockHash = chain.BlockHashes[height];
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
        => TryGetBlock(ChainId, height, out block);

    public bool TryGetBlock(Guid chainId, int height, [MaybeNullWhen(false)] out Block block)
    {
        var chain = Chains[chainId];
        if (chain.BlockHashes.TryGetValue(height, out var blockHash))
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

    public Block? GetBlockOrDefault(int height) => GetBlockOrDefault(ChainId, height);

    public Block? GetBlockOrDefault(Guid chainId, int height)
    {
        var chain = Chains[chainId];
        if (chain.BlockHashes.TryGetValue(height, out var blockHash))
        {
            return GetBlockOrDefault(blockHash);
        }

        return null;
    }

    public long GetNonce(Address address) => GetNonce(ChainId, address);

    public long GetNonce(Guid chainId, Address address)
    {
        var chain = Chains[chainId];
        if (chain.Nonces.TryGetValue(address, out var nonce))
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
