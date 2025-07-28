using System.Reactive;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Types;

namespace Libplanet.Net.Tasks;

internal sealed class ConsumeBlockCandidatesTask(Blockchain blockchain, BlockBranchCollection blockBranches)
    : BackgroundServiceBase
{
    private readonly Subject<Unit> _blockAppendedSubject = new();

    public ConsumeBlockCandidatesTask(Swarm swarm)
        : this(swarm.Blockchain, swarm.BlockBranches)
    {
    }

    public IObservable<Unit> BlockAppended => _blockAppendedSubject;

    protected override TimeSpan GetInterval()
    {
        return TimeSpan.FromMilliseconds(10);
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        _blockAppendedSubject.OnCompleted();
        await base.DisposeAsyncCore();
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var tip = blockchain.Tip;
        if (blockBranches.TryGetValue(tip.Header, out var blockBranch))
        {
            try
            {
                await AppendBranchAsync(blockBranch, tip, cancellationToken);
                _blockAppendedSubject.OnNext(Unit.Default);
            }
            catch (Exception e)
            {
                _blockAppendedSubject.OnError(e);
            }
            finally
            {
                blockBranches.Remove(tip.Header);
            }
        }

        // blockBranches.Prune();
    }

    private async ValueTask AppendBranchAsync(BlockBranch blockBranch, Block branchPoint, CancellationToken cancellationToken)
    {
        // var actualBranch = blockBranch.TakeAfter(branchPoint);

        // for (var i = 0; i < actualBranch.Blocks.Length; i++)
        // {
        //     cancellationToken.ThrowIfCancellationRequested();
        //     blockchain.Append(actualBranch.Blocks[i], actualBranch.BlockCommits[i]);
        //     await Task.Yield();
        // }
    }
}
