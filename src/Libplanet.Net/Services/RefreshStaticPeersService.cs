using System.Reactive.Subjects;
using Libplanet.Net.Components;

namespace Libplanet.Net.Services;

internal sealed class RefreshStaticPeersService(
    PeerExplorer peerExplorer, ImmutableArray<Peer> staticPeers) : PeriodicTaskService
{
    private readonly Subject<Peer> _peerAddedSubject = new();

    public IObservable<Peer> PeerAdded => _peerAddedSubject;

    public TimeSpan StaticPeersMaintainPeriod { get; } = TimeSpan.FromSeconds(10);

    protected override bool CanExecute => staticPeers.Length > 0;

    protected override TimeSpan GetInterval() => StaticPeersMaintainPeriod;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var peers = staticPeers.Except(peerExplorer.Peers);
        var addedPeers = await peerExplorer.PingManyAsync([.. peers], cancellationToken);
        foreach (var peer in addedPeers)
        {
            _peerAddedSubject.OnNext(peer);
        }
    }
}
