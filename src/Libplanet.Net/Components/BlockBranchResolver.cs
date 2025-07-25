using System.Collections.Concurrent;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Services;
using Libplanet.Types.Threading;

namespace Libplanet.Net.Components;

public sealed class BlockBranchResolver(Blockchain blockchain, ITransport transport)
    : IDisposable
{
    private readonly Subject<BlockBranch> _blockBranchCreatedSubject = new();
    private readonly BlockFetcher _blockFetcher = new(blockchain, transport);
    private readonly ConcurrentDictionary<Peer, int> _processByPeer = new();
    private bool _disposed;

    public IObservable<BlockBranch> BlockBranchCreated => _blockBranchCreatedSubject;

    public BlockBranchCollection BlockBranches { get; } = [];

    public void Dispose()
    {
        if (!_disposed)
        {
            _blockBranchCreatedSubject.Dispose();
            _blockFetcher.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    public async Task ExecuteAsync(BlockDemandCollection blockDemands, CancellationToken cancellationToken)
    {
        var taskList = new List<Task>(blockDemands.Count);
        foreach (var blockDemand in blockDemands)
        {
            blockDemands.Remove(blockDemand.Peer);
            taskList.Add(ProcessBlockDemandAsync(blockDemand, cancellationToken));
        }

        await TaskUtility.TryWhenAll(taskList);
    }

    private async Task ProcessBlockDemandAsync(BlockDemand blockDemand, CancellationToken cancellationToken)
    {
        var peer = blockDemand.Peer;
        if (blockDemand.Height <= blockchain.Tip.Height)
        {
            return;
        }

        if (!_processByPeer.TryAdd(peer, 0))
        {
            return;
        }

        try
        {
            var tip = blockchain.Tip;
            var blockPairs = await _blockFetcher.FetchAsync(peer, tip.BlockHash, cancellationToken);
            var blockBranch = new BlockBranch
            {
                BlockHeader = tip.Header,
                Blocks = [.. blockPairs.Select(item => item.Item1)],
                BlockCommits = [.. blockPairs.Select(item => item.Item2)],
            };
            BlockBranches.Add(tip.Header, blockBranch);
            _blockBranchCreatedSubject.OnNext(blockBranch);
        }
        catch (Exception e)
        {
            _blockBranchCreatedSubject.OnError(e);
        }
        finally
        {
            _processByPeer.TryRemove(peer, out _);
        }
    }
}
