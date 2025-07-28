using System.Threading;
using System.Threading.Tasks;

namespace Libplanet.Net.Tasks;

internal sealed class TxBroadcastTask(Swarm swarm) : BackgroundServiceBase
{
    private readonly Blockchain _blockchain = swarm.Blockchain;

    protected override TimeSpan GetInterval()
    {
        return swarm.Options.TxBroadcastInterval;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var stagedTransactions = _blockchain.StagedTransactions;
        var txIds = stagedTransactions.Keys.ToArray();
        if (txIds.Length > 0)
        {
            swarm.BroadcastTxIds(default, [.. txIds]);
        }

        await Task.CompletedTask;
    }
}
