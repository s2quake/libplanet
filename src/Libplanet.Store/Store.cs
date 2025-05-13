namespace Libplanet.Store;

public sealed class Store : IDisposable
{
    private readonly IDatabase _database;
    private readonly TransactionStore _transactions;
    private readonly TxExecutionStore _txExecutions;
    private readonly BlockDigestStore _blockDigests;
    private readonly BlockCommitStore _blockCommits;
    private readonly PendingEvidenceStore _pendingEvidences;
    private readonly CommittedEvidenceStore _committedEvidences;
    private readonly ChainStore _chains;
    private readonly MetadataStore _metadata;
    private readonly BlockHashesByTxId _blockHashesByTxId;
    private Chain? _chain;

    private bool _disposed;

    public Store(IDatabase database)
    {
        _database = database;
        _transactions = new TransactionStore(_database);
        _blockDigests = new BlockDigestStore(_database);
        _txExecutions = new TxExecutionStore(_database);
        _blockCommits = new BlockCommitStore(_database);
        _pendingEvidences = new PendingEvidenceStore(_database);
        _committedEvidences = new CommittedEvidenceStore(_database);
        _chains = new ChainStore(_database);
        _metadata = new MetadataStore(_database);
        _blockHashesByTxId = new BlockHashesByTxId(_database);
        if (_metadata.TryGetValue("chainId", out var chainId))
        {
            _chain = _chains[Guid.Parse(chainId)];
        }
    }

    public PendingEvidenceStore PendingEvidences => _pendingEvidences;

    public CommittedEvidenceStore CommittedEvidences => _committedEvidences;

    public TransactionStore Transactions => _transactions;

    public BlockCommitStore BlockCommits => _blockCommits;

    public BlockDigestStore BlockDigests => _blockDigests;

    public TxExecutionStore TxExecutions => _txExecutions;

    public ChainStore Chains => _chains;

    public BlockHashesByTxId BlockHashesByTxId => _blockHashesByTxId;

    public Guid ChainId
    {
        get => _metadata.TryGetValue("chainId", out var chainId) ? Guid.Parse(chainId) : Guid.Empty;
        set
        {
            _chain = _chains[value];
            _metadata["chainId"] = value.ToString();
        }
    }

    public Chain Chain => _chain ?? throw new InvalidOperationException(
        "ChainId is not set. Please set ChainId before accessing the Chain property.");

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
