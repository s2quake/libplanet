using Libplanet.Net.MessageHandlers;

namespace Libplanet.Net.Components;

public sealed class TransactionBroadcastingResponder(
    ITransport transport, TransactionDemandCollection transactionDemands)
    : IDisposable
{
    private readonly IDisposable _handlerRegistration = transport.MessageRouter.RegisterMany(
    [
        new TxIdMessageHandler(transactionDemands),
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
