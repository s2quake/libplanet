using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Components;
using Libplanet.Net.Consensus;

namespace Libplanet.Net.Services;

public sealed class MessageBroadcastService(PeerExplorer peerExplorer, Gossip gossip)
    : ServiceBase
{
    private readonly Subject<(ImmutableArray<Peer>, ImmutableArray<MessageId>)> _broadcastedSubject = new();
    private TimeSpan _broadcastInterval = TimeSpan.FromSeconds(1);
    private MessageBroadcaster? _messageBroadcaster;
    private IDisposable? _subscription;

    public IObservable<(ImmutableArray<Peer> Peers, ImmutableArray<MessageId> MessageIds)> Broadcasted
        => _broadcastedSubject;

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
            if (_messageBroadcaster is not null)
            {
                _messageBroadcaster.BroadcastInterval = value;
            }
        }
    }

    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        _messageBroadcaster = new MessageBroadcaster(peerExplorer, gossip.Messages);
        _subscription = _messageBroadcaster.Broadcasted.Subscribe(_broadcastedSubject.OnNext);
        await Task.CompletedTask;
    }

    protected override async Task OnStopAsync(CancellationToken cancellationToken)
    {
        _subscription?.Dispose();
        _subscription = null;
        if (_messageBroadcaster is not null)
        {
            await _messageBroadcaster.DisposeAsync();
            _messageBroadcaster = null;
        }
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        _subscription?.Dispose();
        _subscription = null;
        if (_messageBroadcaster is not null)
        {
            await _messageBroadcaster.DisposeAsync();
            _messageBroadcaster = null;
        }

        _broadcastedSubject.Dispose();
        await base.DisposeAsyncCore();
    }
}
