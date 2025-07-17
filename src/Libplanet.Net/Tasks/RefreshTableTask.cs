using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Protocols;

namespace Libplanet.Net.Tasks;

internal sealed class RefreshTableTask(PeerDiscovery peerDiscovery, TimeSpan refreshInterval, TimeSpan staleThreshold)
    : BackgroundServiceBase
{
    protected override TimeSpan Interval => refreshInterval;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // var maxAge = swarm.Options.RefreshLifespan;
        // var PeerDiscovery = swarm.PeerDiscovery;
        await peerDiscovery.RefreshPeersAsync(staleThreshold, cancellationToken);
        await peerDiscovery.CheckReplacementCacheAsync(cancellationToken);
    }
}
