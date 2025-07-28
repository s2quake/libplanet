using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Components;

namespace Libplanet.Net.Services;

public sealed class BlockchainSynchronizationResponderService(Blockchain blockchain, ITransport transport)
    : ServiceBase
{
    private BlockFetchingHandler? _blockFetchingHandler;

    public int MaxAccessCount { get; set; } = 3;

    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        _blockFetchingHandler = new BlockFetchingHandler(blockchain, transport, MaxAccessCount);
        await Task.CompletedTask;
    }

    protected override async Task OnStopAsync(CancellationToken cancellationToken)
    {
        _blockFetchingHandler?.Dispose();
        _blockFetchingHandler = null;
        await Task.CompletedTask;
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        _blockFetchingHandler?.Dispose();
        _blockFetchingHandler = null;
        await base.DisposeAsyncCore();
    }
}
