using Libplanet.Net.Messages;

namespace Libplanet.Net.MessageHandlers;

internal sealed class EnsurePongScope(ITransport transport, MessageEnvelope messageEnvelope) : IDisposable
{
    public void Dispose() => transport.Pong(messageEnvelope);
}
