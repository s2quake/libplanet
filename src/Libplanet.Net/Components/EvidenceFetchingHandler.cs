using Libplanet.Net.MessageHandlers;

namespace Libplanet.Net.Components;

public sealed class EvidenceFetchingHandler(Blockchain blockchain, ITransport transport, int maxAccessCount)
    : IDisposable
{
    private readonly IDisposable _handlerRegistration = transport.MessageRouter.RegisterMany(
    [
        new EvidenceRequestMessageHandler(blockchain, transport, maxAccessCount),
    ]);
    private bool _disposed;

    public EvidenceFetchingHandler(Blockchain blockchain, ITransport transport)
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
