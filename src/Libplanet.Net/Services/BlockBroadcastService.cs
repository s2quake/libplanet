using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Components;
using Libplanet.Types;

namespace Libplanet.Net.Services;

public sealed class BlockBroadcastService(Blockchain blockchain, PeerExplorer peerExplorer)
    : ServiceBase
{
    private readonly Subject<(ImmutableArray<Peer>, Block)> _broadcastedSubject = new();
    private BlockBroadcaster? _blockBroadcaster;
    private IDisposable? _subscription;

    public IObservable<(ImmutableArray<Peer>, Block)> Broadcasted => _broadcastedSubject;

    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        _blockBroadcaster = new BlockBroadcaster(blockchain, peerExplorer);
        _subscription = _blockBroadcaster.Broadcasted.Subscribe(_broadcastedSubject.OnNext);
        await Task.CompletedTask;
    }

    protected override async Task OnStopAsync(CancellationToken cancellationToken)
    {
        _subscription?.Dispose();
        _subscription = null;
        _blockBroadcaster?.Dispose();
        _blockBroadcaster = null;
        await Task.CompletedTask;
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        _subscription?.Dispose();
        _subscription = null;
        _blockBroadcaster?.Dispose();
        _blockBroadcaster = null;
        _broadcastedSubject.Dispose();
        await base.DisposeAsyncCore();
    }
}
