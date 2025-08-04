using System.Collections.Concurrent;
using System.Reactive.Subjects;
using Libplanet.Types;
using Libplanet.Types.Threading;

namespace Libplanet.Net.Components;

public sealed class BlockBranchResolver(Blockchain blockchain, BlockFetcher blockFetcher)
    : IDisposable
{
    private readonly Subject<(BlockDemand, BlockBranch)> _blockBranchCreatedSubject = new();
    private readonly Subject<(BlockDemand, Exception)> _blockBranchCreationFailedSubject = new();
    private readonly ConcurrentDictionary<Peer, int> _processByPeer = new();
    private bool _disposed;

    public IObservable<(BlockDemand BlockDemand, BlockBranch BlockBranch)> BlockBranchCreated
        => _blockBranchCreatedSubject;

    public IObservable<(BlockDemand BlockDemand, Exception Exception)> BlockBranchCreationFailed
        => _blockBranchCreationFailedSubject;

    public void Dispose()
    {
        if (!_disposed)
        {
            _blockBranchCreatedSubject.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    public async Task ResolveAsync(BlockDemandCollection blockDemands, Block tip, CancellationToken cancellationToken)
    {
        var taskList = new List<Task>(blockDemands.Count);
        foreach (var blockDemand in blockDemands.Flush(blockchain))
        {
            taskList.Add(ProcessBlockDemandAsync(blockDemand, tip, cancellationToken));
        }

        await TaskUtility.TryWhenAll(taskList);
    }

    private async Task ProcessBlockDemandAsync(
        BlockDemand blockDemand, Block tip, CancellationToken cancellationToken)
    {
        var peer = blockDemand.Peer;
        if (blockDemand.Height <= tip.Height)
        {
            return;
        }

        if (!_processByPeer.TryAdd(peer, 0))
        {
            return;
        }

        try
        {
            var blockPairs = await blockFetcher.FetchAsync(peer, tip.BlockHash, cancellationToken);
            var blockBranch = new BlockBranch
            {
                BlockHeader = tip.Header,
                Blocks = [.. blockPairs.Select(item => item.Item1)],
                BlockCommits = [.. blockPairs.Select(item => item.Item2)],
            };
            _blockBranchCreatedSubject.OnNext((blockDemand, blockBranch));
        }
        catch (Exception e)
        {
            _blockBranchCreationFailedSubject.OnNext((blockDemand, e));
        }
        finally
        {
            _processByPeer.TryRemove(peer, out _);
        }
    }
}
