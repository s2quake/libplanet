using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Protocols;

namespace Libplanet.Net.Services;

internal sealed class RefreshTableService(PeerService peerDiscovery, TimeSpan interval, TimeSpan refreshLifespan)
    : BackgroundServiceBase
{
    protected override TimeSpan Interval { get; } = interval;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await peerDiscovery.RefreshPeersAsync(refreshLifespan, cancellationToken);
        await peerDiscovery.CheckReplacementCacheAsync(cancellationToken);
    }
}
