using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Consensus;

namespace Libplanet.Net.Tasks;

internal sealed class HeartbeatTask(Gossip gossip, TimeSpan heartbeatInterval)
    : BackgroundServiceBase
{
    protected override TimeSpan GetInterval()
    {
        return heartbeatInterval;
    }

    protected override Task ExecuteAsync(CancellationToken cancellationToken)
        => gossip.HeartbeatAsync(cancellationToken);
}
