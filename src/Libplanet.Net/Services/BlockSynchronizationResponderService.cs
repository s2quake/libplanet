using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Components;

namespace Libplanet.Net.Services;

public sealed class BlockSynchronizationResponderService(Blockchain blockchain, ITransport transport)
    : ServiceBase
{
    private BlockFetchingResponder? _blockFetchingResponder;

    public int MaxHashesPerResponse { get; set; } = 100;

    public int MaxConcurrentResponses { get; set; } = 3;

    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        _blockFetchingResponder = new BlockFetchingResponder(blockchain, transport, new()
        {
            MaxHashes = MaxHashesPerResponse,
            MaxConcurrentResponses = MaxConcurrentResponses,
        });
        await Task.CompletedTask;
    }

    protected override async Task OnStopAsync(CancellationToken cancellationToken)
    {
        _blockFetchingResponder?.Dispose();
        _blockFetchingResponder = null;
        await Task.CompletedTask;
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        _blockFetchingResponder?.Dispose();
        _blockFetchingResponder = null;
        await base.DisposeAsyncCore();
    }
}
