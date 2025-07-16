using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Protocols;
using Libplanet.Types;

namespace Libplanet.Net.Tasks;

internal sealed class ConsumeBlockCandidatesTask(Swarm swarm) : BackgroundServiceBase
{
    protected override TimeSpan Interval => TimeSpan.FromMilliseconds(10);

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
         if (swarm.BlockCandidateTable.Count > 0)
            {
                BlockHeader tipHeader = swarm.Blockchain.Tip.Header;
                if (swarm.BlockCandidateTable.GetCurrentRoundCandidate(swarm.Blockchain.Tip) is { } branch)
                {
                    var root = branch.Keys.First();
                    var tip = branch.Keys.Last();
                    _ = swarm.BlockCandidateProcess(
                        branch,
                        cancellationToken);
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
