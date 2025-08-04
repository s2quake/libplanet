using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Components;
using Libplanet.Types;

namespace Libplanet.Net.Services;

public sealed class TransactionBroadcastService(Blockchain blockchain, PeerExplorer peerExplorer)
    : ServiceBase
{
    private readonly Subject<(ImmutableArray<Peer>, ImmutableArray<TxId>)> _broadcastedSubject = new();
    private TimeSpan _broadcastInterval = TimeSpan.FromSeconds(1);
    private TransactionBroadcaster? _transactionBroadcaster;
    private IDisposable? _subscription;

    public IObservable<(ImmutableArray<Peer>, ImmutableArray<TxId>)> Broadcasted => _broadcastedSubject;

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
            if (_transactionBroadcaster is not null)
            {
                _transactionBroadcaster.BroadcastInterval = value;
            }
        }
    }

    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        _transactionBroadcaster = new TransactionBroadcaster(blockchain, peerExplorer);
        _subscription = _transactionBroadcaster.Broadcasted.Subscribe(_broadcastedSubject.OnNext);
        await Task.CompletedTask;
    }

    protected override async Task OnStopAsync(CancellationToken cancellationToken)
    {
        _subscription?.Dispose();
        _subscription = null;
        if (_transactionBroadcaster is not null)
        {
            await _transactionBroadcaster.DisposeAsync();
            _transactionBroadcaster = null;
        }
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        _subscription?.Dispose();
        _subscription = null;
        if (_transactionBroadcaster is not null)
        {
            await _transactionBroadcaster.DisposeAsync();
            _transactionBroadcaster = null;
        }

        _broadcastedSubject.Dispose();
        await base.DisposeAsyncCore();
    }
}
