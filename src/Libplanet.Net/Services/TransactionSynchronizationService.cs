using System.Collections.Specialized;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Components;
using Libplanet.Types;

namespace Libplanet.Net.Services;

internal sealed class TransactionSynchronizationService(
    Blockchain blockchain, PeerExplorer peerExplorer)
    : ServiceBase
{
    private readonly Subject<ImmutableArray<Transaction>> _stagedSubject = new();
    private readonly Blockchain _blockchain = blockchain;
    private readonly ITransport _transport = peerExplorer.Transport;
    private readonly TransactionFetcher _transactionFetcher = new(blockchain, peerExplorer.Transport);
    private TransactionBroadcastingHandler? _transactionBroadcastingHandler;

    public IObservable<ImmutableArray<Transaction>> Staged => _stagedSubject;

    public TransactionDemandCollection TransactionDemands { get; } = new();

    public async Task SynchronizeAsync(CancellationToken cancellationToken)
    {
        var taskList = new List<Task>(TransactionDemands.Count);
        foreach (var demand in TransactionDemands.Flush())
        {
            taskList.Add(ProcessDemandAsync(demand, cancellationToken));
        }

        await Task.WhenAll(taskList);
        // var results = await Task.WhenAll(taskList);
        // var txIds = results.SelectMany(tx => tx).ToImmutableHashSet();
        // _stagedSubject.OnNext([.. txIds.Select(tx => tx.Id)]);
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

        _transactionFetcher.Dispose();
        TransactionDemands.Clear();
        await base.DisposeAsyncCore();
    }

    private async Task ProcessDemandAsync(TransactionDemand demand, CancellationToken cancellationToken)
    {
        var peer = demand.Peer;
        var txIds = demand.TxIds;
        var txs = await _transactionFetcher.FetchAsync(peer, [.. txIds], cancellationToken);
        if (txs.Length == 0)
        {
            return;
        }

        var stagedTxs = new List<Transaction>(txs.Length);
        foreach (var tx in txs)
        {
            if (!_blockchain.Transactions.ContainsKey(tx.Id) && _blockchain.StagedTransactions.TryAdd(tx))
            {
                stagedTxs.Add(tx);
            }
        }

        peerExplorer.Broadcast([.. stagedTxs], new BroadcastOptions { Except = [demand.Peer] });
        _stagedSubject.OnNext([.. stagedTxs]);
    }
}
