using System.Threading;
using System.Threading.Tasks;

namespace Libplanet.Net.Tasks;

internal sealed class RefreshTableTask(PeerService peerService, TimeSpan refreshInterval, TimeSpan staleThreshold)
    : BackgroundServiceBase
{
    protected override TimeSpan Interval => refreshInterval;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // var maxAge = swarm.Options.RefreshLifespan;
        // var PeerDiscovery = swarm.PeerDiscovery;
        await peerService.RefreshAsync(staleThreshold, cancellationToken);
        await peerService.CheckReplacementCacheAsync(cancellationToken);
    }
}
