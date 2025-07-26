using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Components;

namespace Libplanet.Net.Tasks;

internal sealed class RebuildConnectionTask(Swarm swarm) : BackgroundServiceBase
{
    protected override TimeSpan Interval => TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var peerDiscovery = swarm.PeerDiscovery;
        await peerDiscovery.RebuildConnectionAsync(PeerDiscovery.MaxDepth, cancellationToken);
    }
}
