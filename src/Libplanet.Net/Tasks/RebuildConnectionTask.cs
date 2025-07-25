using System.Threading;
using System.Threading.Tasks;

namespace Libplanet.Net.Tasks;

internal sealed class RebuildConnectionTask(Swarm swarm) : BackgroundServiceBase
{
    protected override TimeSpan Interval => TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var PeerDiscovery = swarm.PeerDiscovery;
        await PeerDiscovery.RebuildConnectionAsync(PeerDiscovery.MaxDepth, cancellationToken);
    }
}
