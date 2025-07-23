using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net;

namespace Libplanet.Net.Tasks;

internal sealed class RefreshTableTask(PeerService peerDiscovery, TimeSpan refreshInterval, TimeSpan staleThreshold)
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
