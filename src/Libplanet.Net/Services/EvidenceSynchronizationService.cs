using System.Collections.Specialized;
using System.Reactive.Subjects;
using Libplanet.Net.Components;
using Libplanet.Types;

namespace Libplanet.Net.Services;

internal sealed class EvidenceSynchronizationService(
    Blockchain blockchain, ITransport transport)
    : ServiceBase
{
    private readonly Subject<ImmutableArray<EvidenceBase>> _evidenceAddedSubject = new();
    private readonly Blockchain _blockchain = blockchain;
    private readonly ITransport _transport = transport;
    private readonly EvidenceFetcher _evidenceFetcher = new(blockchain, transport);
    private EvidenceBroadcastingResponder? _transactionBroadcastingHandler;

    public IObservable<ImmutableArray<EvidenceBase>> EvidenceAdded => _evidenceAddedSubject;

    public EvidenceDemandCollection EvidenceDemands { get; } = new();

    public async Task SynchronizeAsync(CancellationToken cancellationToken)
    {
        var taskList = new List<Task>(EvidenceDemands.Count);
        foreach (var demand in EvidenceDemands.Flush())
        {
            taskList.Add(ProcessDemandAsync(demand, cancellationToken));
        }

        await Task.WhenAll(taskList);
    }

    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        _transactionBroadcastingHandler = new EvidenceBroadcastingResponder(_transport, EvidenceDemands);
        EvidenceDemands.CollectionChanged += EvidenceDemands_CollectionChanged;
        await Task.CompletedTask;
    }

    private void EvidenceDemands_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            _ = Task.Run(async () => await SynchronizeAsync(default));
        }
    }

    protected override Task OnStopAsync(CancellationToken cancellationToken)
    {
        EvidenceDemands.CollectionChanged -= EvidenceDemands_CollectionChanged;
        EvidenceDemands.Clear();
        _transactionBroadcastingHandler?.Dispose();
        _transactionBroadcastingHandler = null;
        return Task.CompletedTask;
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        EvidenceDemands.CollectionChanged -= EvidenceDemands_CollectionChanged;
        _transactionBroadcastingHandler?.Dispose();
        _transactionBroadcastingHandler = null;

        _evidenceFetcher.Dispose();
        EvidenceDemands.Clear();
        await base.DisposeAsyncCore();
    }

    private async Task ProcessDemandAsync(EvidenceDemand demand, CancellationToken cancellationToken)
    {
        var peer = demand.Peer;
        var evidenceIds = demand.EvidenceIds;
        var evidences = await _evidenceFetcher.FetchAsync(peer, [.. evidenceIds], cancellationToken);
        if (evidences.Length == 0)
        {
            return;
        }

        var pendingEvidences = new List<EvidenceBase>(evidences.Length);
        foreach (var evidence in evidences)
        {
            if (!_blockchain.Evidence.ContainsKey(evidence.Id) && _blockchain.PendingEvidence.TryAdd(evidence))
            {
                pendingEvidences.Add(evidence);
            }
        }

        _evidenceAddedSubject.OnNext([.. pendingEvidences]);
    }
}
