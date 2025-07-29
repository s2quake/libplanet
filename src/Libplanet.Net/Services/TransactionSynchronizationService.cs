using System.Collections.Specialized;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Components;
using Libplanet.Types;

namespace Libplanet.Net.Services;

internal sealed class TransactionSynchronizationService : ServiceBase
{
    private readonly Subject<ImmutableArray<TxId>> _synchronizedSubject = new();
    private readonly Blockchain _blockchain;
    private readonly ITransport _transport;
    private readonly TransactionFetcher _transactionFetcher;
    private readonly DisposerCollection _subscriptions;
    private TransactionBroadcastingHandler? _transactionBroadcastingHandler;

    public TransactionSynchronizationService(Blockchain blockchain, ITransport transport)
    {
        _blockchain = blockchain;
        _transport = transport;
        _transactionFetcher = new(blockchain, transport);
        _subscriptions =
        [
            // _blockBranchAppender.BlockBranchAppended.Subscribe(_synchronizedSubject.OnNext),
        ];
    }

    public IObservable<ImmutableArray<TxId>> Synchronized => _synchronizedSubject;

    public TransactionDemandCollection TransactionDemands { get; } = new();

    public async Task SynchronizeAsync(CancellationToken cancellationToken)
    {
        var taskList = new List<Task<ImmutableArray<Transaction>>>(TransactionDemands.Count);
        foreach (var demand in TransactionDemands.Flush())
        {
            taskList.Add(ProcessDemandAsync(demand, cancellationToken));
        }

        var results = await Task.WhenAll(taskList);
        var txIds = results.SelectMany(tx => tx).ToImmutableHashSet();
        _synchronizedSubject.OnNext([.. txIds.Select(tx => tx.Id)]);
    }

    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        _transactionBroadcastingHandler = new TransactionBroadcastingHandler(_transport, TransactionDemands);
        TransactionDemands.CollectionChanged += TransactionDemands_CollectionChanged;
        await Task.CompletedTask;
    }

    private void TransactionDemands_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            _ = Task.Run(async () => await SynchronizeAsync(default));
        }
    }

    protected override Task OnStopAsync(CancellationToken cancellationToken)
    {
        TransactionDemands.CollectionChanged -= TransactionDemands_CollectionChanged;
        TransactionDemands.Clear();
        _transactionBroadcastingHandler?.Dispose();
        _transactionBroadcastingHandler = null;
        return Task.CompletedTask;
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        TransactionDemands.CollectionChanged -= TransactionDemands_CollectionChanged;
        _transactionBroadcastingHandler?.Dispose();
        _transactionBroadcastingHandler = null;

        _subscriptions.Dispose();
        _transactionFetcher.Dispose();
        TransactionDemands.Clear();
        await base.DisposeAsyncCore();
    }

    private async Task<ImmutableArray<Transaction>> ProcessDemandAsync(TransactionDemand demand, CancellationToken cancellationToken)
    {
        var peer = demand.Peer;
        var txIds = demand.TxIds;
        var txs = await _transactionFetcher.FetchAsync(peer, [.. txIds], cancellationToken);
        var stagedTxs = new List<Transaction>(txs.Length);
        foreach (var tx in txs)
        {
            if (_blockchain.StagedTransactions.TryAdd(tx))
            {
                stagedTxs.Add(tx);
            }
        }

        return [.. stagedTxs];
    }
}
