using System.Threading;
using System.Threading.Tasks;

namespace Libplanet.Net.Tasks;

internal sealed class TxBroadcastTask(Swarm swarm) : SwarmTaskBase
{
    protected override TimeSpan Interval => swarm.Options.TxBroadcastInterval;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var blockchain = swarm.Blockchain;
        var txIds = blockchain.StagedTransactions.Keys.ToArray();
        if (txIds.Length > 0)
        {
            swarm.BroadcastTxIds(default, txIds);
        }

        await Task.CompletedTask;
    }
}
