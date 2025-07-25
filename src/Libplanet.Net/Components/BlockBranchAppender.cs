using System.Threading;
using System.Threading.Tasks;
using Libplanet.Types.Threading;

namespace Libplanet.Net.Components;

public sealed class BlockBranchAppender(Blockchain blockchain)
{
    public async Task ExecuteAsync(BlockBranchCollection blockBranches, CancellationToken cancellationToken)
    {
        var taskList = new List<Task>(blockBranches.Count);
        foreach (var blockBranch in blockBranches)
        {
            blockBranches.Remove(blockBranch.BlockHeader);
            taskList.Add(AppendBranchAsync(blockBranch, cancellationToken));
        }

        blockBranches.Prune(blockchain);
        await TaskUtility.TryWhenAll(taskList);
    }

    private async Task AppendBranchAsync(BlockBranch blockBranch, CancellationToken cancellationToken)
    {
        for (var i = 0; i < blockBranch.Blocks.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            blockchain.Append(blockBranch.Blocks[i], blockBranch.BlockCommits[i]);
            await Task.Yield();
        }
    }
}
