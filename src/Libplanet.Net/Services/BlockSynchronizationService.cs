using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Components;

namespace Libplanet.Net.Services;

internal sealed class BlockSynchronizationService : ServiceBase
{
    private readonly Subject<BlockBranch> _appendedSubject = new();
    private readonly Subject<(BlockBranch, Exception)> _appendingFailedSubject = new();
    private readonly Subject<(BlockDemand, BlockBranch)> _fetchedSubject = new();
    private readonly Subject<(BlockDemand, Exception)> _fetchingFailedSubject = new();
    private readonly Blockchain _blockchain;
    private readonly ITransport _transport;
    private readonly BlockFetcher _blockFetcher;
    private readonly BlockBranchResolver _blockBranchResolver;
    private readonly BlockBranchAppender _blockBranchAppender;
    private readonly DisposerCollection _subscriptions;
    private BlockBroadcastingHandler? _blockBroadcastingHandler;

    public BlockSynchronizationService(Blockchain blockchain, ITransport transport)
    {
        _blockchain = blockchain;
        _transport = transport;
        _blockFetcher = new(blockchain, transport);
        _blockBranchResolver = new(blockchain, _blockFetcher) { BlockBranches = BlockBranches };
        _blockBranchAppender = new(blockchain);
        _subscriptions =
        [
            _blockBranchAppender.BlockBranchAppended.Subscribe(_appendedSubject.OnNext),
            _blockBranchAppender.BlockBranchAppendFailed.Subscribe(_appendingFailedSubject.OnNext),
            _blockBranchResolver.BlockBranchCreated.Subscribe(_fetchedSubject.OnNext),
            _blockBranchResolver.BlockBranchCreationFailed.Subscribe(_fetchingFailedSubject.OnNext),
            BlockDemands.Added.Subscribe(BlockDemandsAdded),
        ];
    }

    public IObservable<BlockBranch> Appended => _appendedSubject;

    public IObservable<(BlockBranch, Exception)> AppendingFailed => _appendingFailedSubject;

    public IObservable<(BlockDemand, BlockBranch)> Fetched => _fetchedSubject;

    public IObservable<(BlockDemand, Exception)> FetchingFailed => _fetchingFailedSubject;

    public BlockDemandCollection BlockDemands { get; } = new();

    public BlockBranchCollection BlockBranches { get; } = [];

    public async Task SynchronizeAsync(CancellationToken cancellationToken)
    {
        await _blockBranchResolver.ResolveAsync(BlockDemands, _blockchain.Tip, cancellationToken);
        await _blockBranchAppender.AppendAsync(BlockBranches, cancellationToken);
    }

    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        _blockBroadcastingHandler = new BlockBroadcastingHandler(_blockchain, _transport, BlockDemands);
        await Task.CompletedTask;
    }

    protected override Task OnStopAsync(CancellationToken cancellationToken)
    {
        _blockBroadcastingHandler?.Dispose();
        _blockBroadcastingHandler = null;
        return Task.CompletedTask;
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        _blockBroadcastingHandler?.Dispose();
        _blockBroadcastingHandler = null;

        _subscriptions.Dispose();
        _blockBranchResolver.Dispose();
        _blockFetcher.Dispose();
        BlockDemands.Clear();
        await base.DisposeAsyncCore();
    }

    private void BlockDemandsAdded(BlockDemand demand)
    {
        if (IsRunning)
        {
            _ = Task.Run(async () => await SynchronizeAsync(default));
        }
    }
}
