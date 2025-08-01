using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Components;

namespace Libplanet.Net.Services;

internal sealed class ExplorePeersService(PeerExplorer peerExplorer, TimeSpan interval)
    : PeriodicTaskService
{
    private readonly TimeSpan interval = interval;

    protected override TimeSpan GetInterval()
    {
        return interval;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await peerExplorer.ExploreAsync(cancellationToken);
    }
}
