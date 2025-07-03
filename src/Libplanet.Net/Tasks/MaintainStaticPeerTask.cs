using System.Threading;
using System.Threading.Tasks;

namespace Libplanet.Net.Tasks;

internal sealed class MaintainStaticPeerTask(Swarm swarm) : SwarmTaskBase
{
    public override bool IsEnabled => !swarm.Options.StaticPeers.IsEmpty;

    protected override TimeSpan Interval => TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var tasks = swarm.Options.StaticPeers
            .Where(peer => !swarm.RoutingTable.Contains(peer))
            .Select(async peer =>
            {
                try
                {
                    var timeout = TimeSpan.FromSeconds(3);
                    await swarm.AddPeersAsync([peer], cancellationToken).WaitAsync(timeout, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // do nothing
                }
            });
        await Task.WhenAll(tasks);
    }
}
