using System.Threading;
using System.Threading.Tasks;

namespace Libplanet.Net.Tasks;

internal sealed class RefreshTableTask(Swarm swarm) : SwarmTaskBase
{
    protected override TimeSpan Interval => swarm.Options.RefreshPeriod;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var maxAge = swarm.Options.RefreshLifespan;
        var PeerDiscovery = swarm.PeerDiscovery;
        await PeerDiscovery.RefreshTableAsync(maxAge, cancellationToken);
        await PeerDiscovery.CheckReplacementCacheAsync(cancellationToken);
    }
}
