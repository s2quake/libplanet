using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Components;

namespace Libplanet.Net.Services;

public sealed class TransactionSynchronizationResponderService(Blockchain blockchain, ITransport transport)
    : ServiceBase
{
    private TransactionFetchingHandler? _transactionFetchingHandler;

    public int MaxAccessCount { get; set; } = 3;

    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        _transactionFetchingHandler = new TransactionFetchingHandler(blockchain, transport, MaxAccessCount);
        await Task.CompletedTask;
    }

    protected override async Task OnStopAsync(CancellationToken cancellationToken)
    {
        _transactionFetchingHandler?.Dispose();
        _transactionFetchingHandler = null;
        await Task.CompletedTask;
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        _transactionFetchingHandler?.Dispose();
        _transactionFetchingHandler = null;
        await base.DisposeAsyncCore();
    }
}
