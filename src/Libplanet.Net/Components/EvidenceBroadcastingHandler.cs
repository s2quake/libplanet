using Libplanet.Net.MessageHandlers;

namespace Libplanet.Net.Components;

public sealed class EvidenceBroadcastingHandler(
    ITransport transport, EvidenceDemandCollection evidenceDemands)
    : IDisposable
{
    private readonly IDisposable _handlerRegistration = transport.MessageRouter.RegisterMany(
    [
        new EvidenceIdMessageHandler(evidenceDemands),
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
