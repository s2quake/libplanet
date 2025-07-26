using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Components;

namespace Libplanet.Net.Services;

public sealed class BlockBranchService(
    Blockchain blockchain,
    ITransport transport,
    BlockBranchCollection blockBranches,
    BlockDemandCollection blockDemands)
    : BackgroundServiceBase
{
    private readonly Subject<BlockBranch> _blockBranchCreatedSubject = new();
    private readonly BlockFetcher _blockFetcher = new(blockchain, transport);
    private readonly BlockBranchResolver _blockBranchResolver = new(blockchain, transport)
    {
        BlockBranches = blockBranches,
    };
    private readonly BlockBranchAppender _blockBranchAppender = new(blockchain);

    internal BlockBranchService(Swarm swarm)
        : this(swarm.Blockchain, swarm.Transport, swarm.BlockBranches, swarm.BlockDemands)
    {
    }

    public IObservable<BlockBranch> BlockBranchCreated => _blockBranchCreatedSubject;

    protected override TimeSpan Interval => TimeSpan.FromMilliseconds(100);

    protected override async ValueTask DisposeAsyncCore()
    {
        _blockFetcher.Dispose();
        _blockBranchResolver.Dispose();
        _blockBranchCreatedSubject.Dispose();
        await base.DisposeAsyncCore();
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await _blockBranchResolver.ExecuteAsync(blockDemands, cancellationToken);
        await _blockBranchAppender.ExecuteAsync(blockBranches, cancellationToken);
    }
}
