using System.Threading;
using System.Threading.Tasks;
using Libplanet.Types.Threading;

namespace Libplanet.Net;

public sealed class BlockBranchAppendService(Blockchain blockchain) : ServiceBase
{
    public async Task ExecuteAsync(BlockBranchCollection blockBranches, CancellationToken cancellationToken)
    {
        using var cancellationTokenSource = CreateCancellationTokenSource(cancellationToken);
        var taskList = new List<Task>(blockBranches.Count);
        foreach (var blockBranch in blockBranches)
        {
            blockBranches.Remove(blockBranch.BlockHeader);
            taskList.Add(AppendBranchAsync(blockBranch, cancellationTokenSource.Token));
        }

        blockBranches.Prune();
        await TaskUtility.TryWhenAll(taskList);
    }

    protected override Task OnStartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    protected override Task OnStopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task AppendBranchAsync(BlockBranch blockBranch, CancellationToken cancellationToken)
    {
        // var branchPoint = blockchain.Tip;
        // var actualBranch = blockBranch.TakeAfter(branchPoint);

        // var index = blockBranch.Blocks.IndexOf(branchPoint);
        // var s = index >= 0 ? index + 1 : int.MaxValue;
        // var blocks = blockBranch.Blocks[s..];
        // var blockCommits = blockBranch.BlockCommits[s..];
        // return new BlockBranch
        // {
        //     Blocks = ,
        //     BlockCommits = BlockCommits[i..],
        // };

        for (var i = 0; i < blockBranch.Blocks.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            blockchain.Append(blockBranch.Blocks[i], blockBranch.BlockCommits[i]);
            await Task.Yield();
        }
    }
}
