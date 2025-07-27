using System.Threading;
using System.Threading.Tasks;

namespace Libplanet.Net.Tasks;

internal sealed class MaintainStaticPeerTask(Swarm swarm) : BackgroundServiceBase
{
    protected override TimeSpan Interval => swarm.Options.StaticPeersMaintainPeriod;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (swarm.Options.StaticPeers.IsEmpty)
        {
            return;
        }

        var peerService = swarm.PeerExplorer;

        var tasks = swarm.Options.StaticPeers
            .Where(peer => !swarm.PeerExplorer.Peers.Contains(peer))
            .Select(async peer =>
            {
                try
                {
                    var timeout = TimeSpan.FromSeconds(3);
                    await peerService.AddOrUpdateManyAsync([peer], cancellationToken).WaitAsync(timeout, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // do nothing
                }
            });
        await Task.WhenAll(tasks);
    }
}
