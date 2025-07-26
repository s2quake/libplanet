using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Components;

namespace Libplanet.Net.Services;

internal sealed class RefreshTableService(PeerExplorer peerService, TimeSpan interval, TimeSpan refreshLifespan)
    : BackgroundServiceBase
{
    protected override TimeSpan Interval { get; } = interval;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await peerService.RefreshAsync(refreshLifespan, cancellationToken);
        await peerService.CheckReplacementCacheAsync(cancellationToken);
    }
}
