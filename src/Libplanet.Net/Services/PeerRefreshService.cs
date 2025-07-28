using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Components;

namespace Libplanet.Net.Services;

internal sealed class PeerRefreshService(PeerExplorer peerExplorer, TimeSpan interval, TimeSpan refreshLifespan)
    : BackgroundServiceBase
{
    private readonly TimeSpan interval = interval;

    protected override TimeSpan GetInterval()
    {
        return interval;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await peerExplorer.RefreshAsync(refreshLifespan, cancellationToken);
        await peerExplorer.CheckReplacementCacheAsync(cancellationToken);
    }
}
