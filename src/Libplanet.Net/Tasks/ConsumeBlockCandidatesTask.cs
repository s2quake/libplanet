using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net;
using Libplanet.Types;

namespace Libplanet.Net.Tasks;

internal sealed class ConsumeBlockCandidatesTask(Swarm swarm) : BackgroundServiceBase
{
    protected override TimeSpan Interval => TimeSpan.FromMilliseconds(10);

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var blockBranches = swarm.BlockBranches;
        if (blockBranches.Count > 0)
        {
            BlockHeader tipHeader = swarm.Blockchain.Tip.Header;
            if (blockBranches.TryGetValue(swarm.Blockchain.Tip.BlockHash, out var blockBranch))
            {
                // var root = blockBranch.Keys.First();
                // var tip = blockBranch.Keys.Last();
                // _ = swarm.BlockCandidateProcessAsync(
                //     blockBranch,
                //     cancellationToken);
                // _blockAppendedSubject.OnNext(Unit.Default);
            }
        }
        // else if (checkInterval is { } interval)
        // {
        //     await Task.Delay(interval, cancellationToken);
        //     continue;
        // }
        // else
        // {
        //     break;
        // }
    }
}
