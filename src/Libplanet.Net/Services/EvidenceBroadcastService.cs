using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Components;
using Libplanet.Types;

namespace Libplanet.Net.Services;

public sealed class EvidenceBroadcastService(Blockchain blockchain, PeerExplorer peerExplorer)
    : ServiceBase
{
    private readonly Subject<(ImmutableArray<Peer>, ImmutableArray<EvidenceId>)> _broadcastedSubject = new();
    private TimeSpan _broadcastInterval = TimeSpan.FromSeconds(1);
    private EvidenceBroadcaster? _evidenceBroadcaster;
    private IDisposable? _subscription;


    public IObservable<(ImmutableArray<Peer>, ImmutableArray<EvidenceId>)> Broadcasted => _broadcastedSubject;

    public TimeSpan BroadcastInterval
    {
        get => _broadcastInterval;
        set
        {
            if (_broadcastInterval < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Broadcast interval must be non-negative.");
            }

            _broadcastInterval = value;
            if (_evidenceBroadcaster is not null)
            {
                _evidenceBroadcaster.BroadcastInterval = value;
            }
        }
    }

    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        _evidenceBroadcaster = new EvidenceBroadcaster(blockchain, peerExplorer);
        _subscription = _evidenceBroadcaster.Broadcasted.Subscribe(_broadcastedSubject.OnNext);
        await Task.CompletedTask;
    }

    protected override async Task OnStopAsync(CancellationToken cancellationToken)
    {
        _subscription?.Dispose();
        _subscription = null;
        if (_evidenceBroadcaster is not null)
        {
            await _evidenceBroadcaster.DisposeAsync();
            _evidenceBroadcaster = null;
        }
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        _subscription?.Dispose();
        _subscription = null;
        if (_evidenceBroadcaster is not null)
        {
            await _evidenceBroadcaster.DisposeAsync();
            _evidenceBroadcaster = null;
        }

        _broadcastedSubject.Dispose();
        await base.DisposeAsyncCore();
    }
}
