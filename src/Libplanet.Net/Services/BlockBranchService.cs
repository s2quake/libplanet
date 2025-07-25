using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace Libplanet.Net.Services;

internal sealed class BlockBranchService(
    Blockchain blockchain,
    ITransport transport,
    BlockBranchCollection blockBranches,
    BlockDemandCollection blockDemands)
    : BackgroundServiceBase
{
    private readonly Subject<BlockBranch> _blockBranchCreatedSubject = new();
    private readonly BlockFetcher _blockFetcher = new(blockchain, transport);
    private readonly ConcurrentDictionary<Peer, int> _processByPeer = new();

    public BlockBranchService(Swarm swarm)
        : this(swarm.Blockchain, swarm.Transport, swarm.BlockBranches, swarm.BlockDemands)
    {
    }

    public IObservable<BlockBranch> BlockBranchCreated => _blockBranchCreatedSubject;

    protected override TimeSpan Interval => TimeSpan.FromMilliseconds(100);

    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        await base.OnStartAsync(cancellationToken);
        await _blockFetcher.StartAsync(cancellationToken);
    }

    protected override async Task OnStopAsync(CancellationToken cancellationToken)
    {
        await _blockFetcher.StopAsync(cancellationToken);
        await base.OnStopAsync(cancellationToken);
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        await _blockFetcher.DisposeAsync();
        _blockBranchCreatedSubject.Dispose();
        await base.DisposeAsyncCore();
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        foreach (var blockDemand in blockDemands)
        {
            blockDemands.Remove(blockDemand.Peer);
            _ = ProcessBlockDemandAsync(blockDemand, cancellationToken);
        }

        blockDemands.RemoveAll(IsBlockNeeded);
        await Task.Yield();
    }

    private bool IsBlockNeeded(BlockSummary target) => target.Height > blockchain.Tip.Height;

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
                Blocks = [.. blockPairs.Select(item => item.Item1)],
                BlockCommits = [.. blockPairs.Select(item => item.Item2)],
            };
            blockBranches.Add(tip.Header, blockBranch);
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
