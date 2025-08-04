using Libplanet.Net.Components;

namespace Libplanet.Net.Services;

internal sealed class RefreshPeersService(PeerExplorer peerExplorer, TimeSpan interval, TimeSpan refreshLifespan)
    : PeriodicTaskService
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
