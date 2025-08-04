using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Components;

namespace Libplanet.Net.Services;

public sealed class EvidenceSynchronizationResponderService(Blockchain blockchain, ITransport transport)
    : ServiceBase
{
    private EvidenceFetchingResponder? _evidenceFetchingHandler;

    public int MaxAccessCount { get; set; } = 3;

    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        _evidenceFetchingHandler = new EvidenceFetchingResponder(blockchain, transport, MaxAccessCount);
        await Task.CompletedTask;
    }

    protected override async Task OnStopAsync(CancellationToken cancellationToken)
    {
        _evidenceFetchingHandler?.Dispose();
        _evidenceFetchingHandler = null;
        await Task.CompletedTask;
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        _evidenceFetchingHandler?.Dispose();
        _evidenceFetchingHandler = null;
        await base.DisposeAsyncCore();
    }
}
