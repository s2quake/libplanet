using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Protocols;

namespace Libplanet.Net.Tasks;

internal sealed class RebuildTableTask(
    PeerService peerDiscovery, ImmutableHashSet<Peer> seeds, TimeSpan rebuildTableInterval)
    : BackgroundServiceBase
{
    protected override TimeSpan Interval => rebuildTableInterval;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await peerDiscovery.BootstrapAsync(seeds, PeerService.MaxDepth, cancellationToken);
    }
}
