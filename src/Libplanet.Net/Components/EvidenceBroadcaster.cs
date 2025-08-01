using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Types;
using Libplanet.Types.Threading;

namespace Libplanet.Net.Components;

public sealed class EvidenceBroadcaster : IAsyncDisposable
{
    private static readonly object _lock = new();
    private readonly Subject<(ImmutableArray<Peer>, ImmutableArray<EvidenceId>)> _broadcastedSubject = new();

    private readonly PeerExplorer _peerExplorer;
    private readonly HashSet<EvidenceId> _broadcastedEvidenceIds;
    private readonly DisposerCollection _subscriptions;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Task _broadcastingTask;
    private bool _disposed;

    public EvidenceBroadcaster(Blockchain blockchain, PeerExplorer peerExplorer)
    {
        _subscriptions =
        [
            blockchain.PendingEvidence.Added.Subscribe(AddInternal),
            blockchain.PendingEvidence.Removed.Subscribe(RemoveInternal),
        ];
        _peerExplorer = peerExplorer;
        _broadcastingTask = BroadcastAsync();
        _broadcastedEvidenceIds = [.. blockchain.PendingEvidence.Keys];
    }

    public IObservable<(ImmutableArray<Peer>, ImmutableArray<EvidenceId>)> Broadcasted => _broadcastedSubject;

    public TimeSpan BroadcastInterval { get; set; } = TimeSpan.FromSeconds(1);

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await _cancellationTokenSource.CancelAsync();
            await TaskUtility.TryWait(_broadcastingTask);
            _cancellationTokenSource.Dispose();
            _subscriptions.Dispose();
            _broadcastedSubject.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    private void AddInternal(EvidenceBase evidence)
    {
        lock (_lock)
        {
            _broadcastedEvidenceIds.Add(evidence.Id);
        }
    }

    private void RemoveInternal(EvidenceBase evidence)
    {
        lock (_lock)
        {
            _broadcastedEvidenceIds.Remove(evidence.Id);
        }
    }

    private ImmutableArray<EvidenceId> FlushInternal()
    {
        lock (_lock)
        {
            var evidenceIds = _broadcastedEvidenceIds.ToImmutableArray();
            _broadcastedEvidenceIds.Clear();
            return evidenceIds;
        }
    }

    private async Task BroadcastAsync()
    {
        while (await TaskUtility.TryDelay(BroadcastInterval, _cancellationTokenSource.Token))
        {
            var evidenceIds = FlushInternal();
            if (evidenceIds.Length > 0)
            {
                var (peers, _) = _peerExplorer.BroadcastEvidence(evidenceIds);
                _broadcastedSubject.OnNext((peers, evidenceIds));
            }
        }
    }
}
