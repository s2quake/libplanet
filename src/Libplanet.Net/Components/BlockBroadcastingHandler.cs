using Libplanet.Net.MessageHandlers;

namespace Libplanet.Net.Components;

public sealed class BlockBroadcastingHandler(
    Blockchain blockchain, ITransport transport, BlockDemandCollection blockDemands)
    : IDisposable
{
    private readonly IDisposable _handlerRegistration = transport.MessageRouter.RegisterMany(
    [
        new BlockSummaryMessageHandler(blockchain, blockDemands),
    ]);
    private bool _disposed;

    public void Dispose()
    {
        if (!_disposed)
        {
            _handlerRegistration.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
