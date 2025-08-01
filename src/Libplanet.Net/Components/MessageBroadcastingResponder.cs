using Libplanet.Net.MessageHandlers;

namespace Libplanet.Net.Components;

public sealed class MessageBroadcastingResponder(
    ITransport transport,
    MessageCollection messages,
    PeerMessageIdCollection peerMessageIds)
    : IDisposable
{
    private readonly IDisposable _handlerRegistration = transport.MessageRouter.RegisterMany(
    [
        new HaveMessageHandler(messages, peerMessageIds),
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
