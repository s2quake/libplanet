using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Components;

namespace Libplanet.Net.Tasks;

internal sealed class MaintainStaticPeerTask(
    PeerExplorer peerExplorer, ImmutableArray<Peer> staticPeers) : PeriodicTaskService
{
    private readonly Subject<Peer> _peerAddedSubject = new();

    public IObservable<Peer> PeerAdded => _peerAddedSubject;

    public TimeSpan StaticPeersMaintainPeriod { get; } = TimeSpan.FromSeconds(10);

    internal MaintainStaticPeerTask(Swarm swarm)
        : this(swarm.PeerExplorer, [.. swarm.Options.StaticPeers])
    {
    }

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
