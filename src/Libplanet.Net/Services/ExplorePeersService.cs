using Libplanet.Net.Components;

namespace Libplanet.Net.Services;

internal sealed class ExplorePeersService(PeerExplorer peerExplorer, TimeSpan interval)
    : PeriodicTaskService
{
    private readonly TimeSpan interval = interval;

    protected override TimeSpan GetInterval() => interval;

    protected override Task ExecuteAsync(CancellationToken cancellationToken)
        => peerExplorer.ExploreAsync(cancellationToken);
}
