using System.Reactive.Subjects;
using Libplanet.Types.Threading;

namespace Libplanet.Net.Components;

public sealed class BlockBranchAppender(Blockchain blockchain) : IDisposable
{
    private readonly Subject<BlockBranch> _blockBranchAppendedSubject = new();
    private readonly Subject<(BlockBranch, Exception)> _blockBranchAppendFailedSubject = new();

    public IObservable<BlockBranch> BlockBranchAppended => _blockBranchAppendedSubject;

    public IObservable<(BlockBranch, Exception)> BlockBranchAppendFailed => _blockBranchAppendFailedSubject;

    public void Dispose()
    {
        _blockBranchAppendedSubject.Dispose();
        _blockBranchAppendFailedSubject.Dispose();
    }

    public async Task AppendAsync(BlockBranchCollection blockBranches, CancellationToken cancellationToken)
    {
        var taskList = new List<Task>(blockBranches.Count);
        foreach (var blockBranch in blockBranches.Flush(blockchain))
        {
            taskList.Add(AppendBranchAsync(blockBranch, cancellationToken));
        }

        await TaskUtility.TryWhenAll(taskList);
    }

    private async Task AppendBranchAsync(BlockBranch blockBranch, CancellationToken cancellationToken)
    {
        try
        {
            for (var i = 0; i < blockBranch.Blocks.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                blockchain.Append(blockBranch.Blocks[i], blockBranch.BlockCommits[i]);
                await Task.Yield();
            }

            _blockBranchAppendedSubject.OnNext(blockBranch);
        }
        catch (Exception e) when (e is not OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            _blockBranchAppendFailedSubject.OnNext((blockBranch, e));
        }
    }
}
