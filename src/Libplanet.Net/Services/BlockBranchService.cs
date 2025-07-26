using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Components;

namespace Libplanet.Net.Services;

public sealed class BlockBranchService : BackgroundServiceBase
{
    private readonly Subject<BlockBranch> _blockBranchCreatedSubject = new();
    private readonly Blockchain _blockchain;
    private readonly BlockFetcher _blockFetcher;
    private readonly BlockBranchResolver _blockBranchResolver;
    private readonly BlockBranchAppender _blockBranchAppender;
    private readonly BlockBranchCollection _blockBranches;
    private readonly BlockDemandCollection _blockDemands;

    public BlockBranchService(
        Blockchain blockchain,
        ITransport transport,
        BlockBranchCollection blockBranches,
        BlockDemandCollection blockDemands)
    {
        _blockchain = blockchain;
        _blockBranches = blockBranches;
        _blockDemands = blockDemands;
        _blockFetcher = new(blockchain, transport);
        _blockBranchResolver = new(_blockFetcher)
        {
            BlockBranches = blockBranches,
        };
        _blockBranchAppender = new(blockchain);
    }

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
        var tip = _blockchain.Tip;
        await _blockBranchResolver.ExecuteAsync(_blockDemands, tip, cancellationToken);
        await _blockBranchAppender.ExecuteAsync(_blockBranches, cancellationToken);
    }
}
