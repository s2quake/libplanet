using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Components;

namespace Libplanet.Net.Tasks;

internal sealed class RebuildTableTask(
    PeerExplorer peerService, ImmutableHashSet<Peer> seeds, TimeSpan rebuildTableInterval)
    : BackgroundServiceBase
{
    protected override TimeSpan Interval => rebuildTableInterval;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // await peerService.BootstrapAsync(seeds, PeerService.MaxDepth, cancellationToken);
    }
}
