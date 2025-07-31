using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Components;

namespace Libplanet.Net.Tasks;

internal sealed class RefreshTableTask(PeerExplorer peerService, TimeSpan refreshInterval, TimeSpan staleThreshold)
    : PeriodicTaskService
{
    protected override TimeSpan GetInterval()
    {
        return refreshInterval;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // var maxAge = swarm.Options.RefreshLifespan;
        // var PeerDiscovery = swarm.PeerDiscovery;
        await peerService.RefreshAsync(staleThreshold, cancellationToken);
        await peerService.CheckReplacementCacheAsync(cancellationToken);
    }
}
