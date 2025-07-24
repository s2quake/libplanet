using Libplanet.Net.Messages;

namespace Libplanet.Net.MessageHandlers;

public readonly struct PongScope(ITransport transport, MessageEnvelope messageEnvelope) : IDisposable
{
    public void Dispose() => transport.Pong(messageEnvelope);
}
