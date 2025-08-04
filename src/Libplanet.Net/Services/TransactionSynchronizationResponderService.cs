using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Components;

namespace Libplanet.Net.Services;

public sealed class TransactionSynchronizationResponderService(Blockchain blockchain, ITransport transport)
    : ServiceBase
{
    private TransactionFetchingResponder? _fetchingResponder;

    public int MaxConcurrentResponses { get; set; } = 3;

    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        _fetchingResponder = new TransactionFetchingResponder(blockchain, transport, MaxConcurrentResponses);
        await Task.CompletedTask;
    }

    protected override async Task OnStopAsync(CancellationToken cancellationToken)
    {
        _fetchingResponder?.Dispose();
        _fetchingResponder = null;
        await Task.CompletedTask;
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        _fetchingResponder?.Dispose();
        _fetchingResponder = null;
        await base.DisposeAsyncCore();
    }
}
