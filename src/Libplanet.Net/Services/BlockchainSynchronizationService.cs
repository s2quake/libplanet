using System.Collections.Specialized;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Components;

namespace Libplanet.Net.Services;

internal sealed class BlockchainSynchronizationService : ServiceBase
{
    private readonly Subject<BlockBranch> _synchronizedSubject = new();
    private readonly Blockchain _blockchain;
    private readonly ITransport _transport;
    private readonly BlockFetcher _blockFetcher;
    private readonly BlockBranchResolver _blockBranchResolver;
    private readonly BlockBranchAppender _blockBranchAppender;
    private readonly DisposerCollection _subscriptions;
    private BlockBroadcastingHandler? _blockBroadcastingHandler;

    public BlockchainSynchronizationService(Blockchain blockchain, ITransport transport)
    {
        _blockchain = blockchain;
        _transport = transport;
        _blockFetcher = new(blockchain, transport);
        _blockBranchResolver = new(blockchain, _blockFetcher) { BlockBranches = BlockBranches };
        _blockBranchAppender = new(blockchain);
        _subscriptions =
        [
            _blockBranchAppender.BlockBranchAppended.Subscribe(_synchronizedSubject.OnNext),
        ];
    }

    public IObservable<BlockBranch> Synchronized => _synchronizedSubject;

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
        BlockDemands.CollectionChanged += BlockDemands_CollectionChanged;
        await Task.CompletedTask;
    }

    private void BlockDemands_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            _ = Task.Run(async () => await SynchronizeAsync(default));
        }
    }

    protected override Task OnStopAsync(CancellationToken cancellationToken)
    {
        BlockDemands.CollectionChanged -= BlockDemands_CollectionChanged;
        BlockDemands.Clear();
        _blockBroadcastingHandler?.Dispose();
        _blockBroadcastingHandler = null;
        return Task.CompletedTask;
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        BlockDemands.CollectionChanged -= BlockDemands_CollectionChanged;
        _blockBroadcastingHandler?.Dispose();
        _blockBroadcastingHandler = null;

        _subscriptions.Dispose();
        _blockBranchResolver.Dispose();
        _blockFetcher.Dispose();
        BlockDemands.Clear();
        await base.DisposeAsyncCore();
    }
}
