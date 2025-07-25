using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Services.Extensions;

namespace Libplanet.Net.Services;

internal sealed class BlockDemandPollService(
    ITransport transport, PeerService peerService, BlockDemandCollection blockDemands)
    : BackgroundServiceBase
{
    internal BlockDemandPollService(Swarm swarm)
        : this(swarm.Transport, swarm.PeerService, swarm.BlockDemands)
    {
    }

    protected override TimeSpan Interval => TimeSpan.FromMilliseconds(100);

    protected override Task ExecuteAsync(CancellationToken cancellationToken)
        => blockDemands.PollAsync(transport, [.. peerService.Peers], cancellationToken);
}
