using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Components;

namespace Libplanet.Net.Services;

internal sealed class PeerRefreshService(PeerExplorer peerExplorer, TimeSpan interval, TimeSpan refreshLifespan)
    : BackgroundServiceBase
{
    protected override TimeSpan Interval { get; } = interval;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await peerExplorer.RefreshAsync(refreshLifespan, cancellationToken);
        await peerExplorer.CheckReplacementCacheAsync(cancellationToken);
    }
}
