using System.Diagnostics.CodeAnalysis;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;

namespace Libplanet.Store;

public sealed class Repository : IDisposable
{
    private readonly IDatabase _database;
    private readonly TxExecutionStore _txExecutions;
    private readonly BlockDigestStore _blockDigests;
    private readonly BlockCommitStore _blockCommits;
    private readonly PendingTransactionStore _pendingTransactions;
    private readonly CommittedTransactionStore _committedTransactions;
    private readonly PendingEvidenceStore _pendingEvidences;
    private readonly CommittedEvidenceStore _committedEvidences;
    private readonly ChainStore _chains;
    private readonly MetadataStore _metadata;
    private Chain? _chain;

    private bool _disposed;

    public Repository(IDatabase database)
    {
        _database = database;
        _blockDigests = new BlockDigestStore(_database);
        _txExecutions = new TxExecutionStore(_database);
        _blockCommits = new BlockCommitStore(_database);
        _pendingTransactions = new PendingTransactionStore(_database);
        _committedTransactions = new CommittedTransactionStore(_database);
        _pendingEvidences = new PendingEvidenceStore(_database);
        _committedEvidences = new CommittedEvidenceStore(_database);
        _chains = new ChainStore(_database);
        _metadata = new MetadataStore(_database);
        if (_metadata.TryGetValue("chainId", out var chainId))
        {
            _chain = _chains[Guid.Parse(chainId)];
        }
    }

    public PendingEvidenceStore PendingEvidences => _pendingEvidences;

    public CommittedEvidenceStore CommittedEvidences => _committedEvidences;

    public PendingTransactionStore PendingTransactions => _pendingTransactions;

    public CommittedTransactionStore CommittedTransactions => _committedTransactions;

    public BlockCommitStore BlockCommits => _blockCommits;

    public BlockDigestStore BlockDigests => _blockDigests;

    public TxExecutionStore TxExecutions => _txExecutions;

    public ChainStore Chains => _chains;

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
                _chain = _chains[value];
                _metadata["chainId"] = value.ToString();
            }
        }
    }

    public Chain Chain => _chain ?? throw new InvalidOperationException(
        "ChainId is not set. Please set ChainId before accessing the Chain property.");

    public void AddBlock(Block block)
    {
        _blockDigests.Add(block);
        _pendingTransactions.AddRange(block.Transactions);
        _pendingEvidences.RemoveRange(block.Evidences);
        _committedEvidences.AddRange(block.Evidences);
    }

    public Block GetBlock(BlockHash blockHash)
    {
        var blockDigest = _blockDigests[blockHash];
        return blockDigest.ToBlock(item => _pendingTransactions[item], item => _committedEvidences[item]);
    }

    public Block GetBlock(int height) => GetBlock(ChainId, height);

    public Block GetBlock(Guid chainId, int height)
    {
        var chain = _chains[chainId];
        var blockHash = chain.BlockHashes[height];
        return GetBlock(blockHash);
    }

    public bool TryGetBlock(BlockHash blockHash, [MaybeNullWhen(false)] out Block block)
    {
        if (_blockDigests.TryGetValue(blockHash, out var blockDigest))
        {
            block = blockDigest.ToBlock(item => _pendingTransactions[item], item => _committedEvidences[item]);
            return true;
        }

        block = null;
        return false;
    }

    public bool TryGetBlock(int height, [MaybeNullWhen(false)] out Block block)
        => TryGetBlock(ChainId, height, out block);

    public bool TryGetBlock(Guid chainId, int height, [MaybeNullWhen(false)] out Block block)
    {
        var chain = _chains[chainId];
        if (chain.BlockHashes.TryGetValue(height, out var blockHash))
        {
            return TryGetBlock(blockHash, out block);
        }

        block = null;
        return false;
    }

    public Block? GetBlockOrDefault(BlockHash blockHash)
    {
        if (_blockDigests.TryGetValue(blockHash, out var blockDigest))
        {
            return blockDigest.ToBlock(item => _pendingTransactions[item], item => _committedEvidences[item]);
        }

        return null;
    }

    public Block? GetBlockOrDefault(int height) => GetBlockOrDefault(ChainId, height);

    public Block? GetBlockOrDefault(Guid chainId, int height)
    {
        var chain = _chains[chainId];
        if (chain.BlockHashes.TryGetValue(height, out var blockHash))
        {
            return GetBlockOrDefault(blockHash);
        }

        return null;
    }

    public long GetNonce(Address address) => GetNonce(ChainId, address);

    public long GetNonce(Guid chainId, Address address)
    {
        var chain = _chains[chainId];
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
