using Libplanet.Net.MessageHandlers;

namespace Libplanet.Net.Components;

public sealed class TransactionFetchingResponder(
    Blockchain blockchain, ITransport transport, int maxConcurrentResponses)
    : IDisposable
{
    private readonly IDisposable _handlerRegistration = transport.MessageRouter.RegisterMany(
    [
        new TransactionRequestMessageHandler(blockchain, transport, maxConcurrentResponses),
    ]);
    private bool _disposed;

    public TransactionFetchingResponder(Blockchain blockchain, ITransport transport)
        : this(blockchain, transport, 3)
    {
    }

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
