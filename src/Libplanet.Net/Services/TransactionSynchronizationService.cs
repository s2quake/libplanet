using System.Collections.Specialized;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Components;
using Libplanet.Types;

namespace Libplanet.Net.Services;

internal sealed class TransactionSynchronizationService(
    Blockchain blockchain, ITransport transport)
    : ServiceBase
{
    private readonly Subject<Transaction> _stagedSubject = new();
    private readonly Subject<(Transaction, Exception)> _stageFailedSubject = new();
    private readonly Blockchain _blockchain = blockchain;
    private readonly ITransport _transport = transport;
    private readonly TransactionFetcher _fetcher = new(blockchain, transport);
    private TransactionBroadcastingResponder? _broadcastingResponder;

    public IObservable<Transaction> Staged => _stagedSubject;

    public TransactionDemandCollection TransactionDemands { get; } = new();

    public async Task SynchronizeAsync(CancellationToken cancellationToken)
    {
        var taskList = new List<Task>(TransactionDemands.Count);
        foreach (var demand in TransactionDemands.Flush())
        {
            taskList.Add(ProcessDemandAsync(demand, cancellationToken));
        }

        await Task.WhenAll(taskList);
    }

    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        _broadcastingResponder = new TransactionBroadcastingResponder(_transport, TransactionDemands);
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
        _broadcastingResponder?.Dispose();
        _broadcastingResponder = null;
        return Task.CompletedTask;
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        TransactionDemands.CollectionChanged -= TransactionDemands_CollectionChanged;
        _broadcastingResponder?.Dispose();
        _broadcastingResponder = null;

        _fetcher.Dispose();
        TransactionDemands.Clear();
        await base.DisposeAsyncCore();
    }

    private async Task ProcessDemandAsync(TransactionDemand demand, CancellationToken cancellationToken)
    {
        var peer = demand.Peer;
        var txIds = demand.TxIds;
        var txs = await _fetcher.FetchAsync(peer, [.. txIds], cancellationToken);
        if (txs.Length == 0)
        {
            return;
        }

        foreach (var tx in txs)
        {
            try
            {
                _blockchain.StagedTransactions.Add(tx);
                _stagedSubject.OnNext(tx);
            }
            catch (Exception e)
            {
                _stageFailedSubject.OnNext((tx, e));
            }
        }
    }
}
