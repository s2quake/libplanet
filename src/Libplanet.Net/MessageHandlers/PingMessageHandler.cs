using Libplanet.Net.Messages;

namespace Libplanet.Net.MessageHandlers;

internal sealed class PingMessageHandler(ITransport transport, params Peer[] peers)
    : MessageHandlerBase<PingMessage>
{
    protected override ValueTask OnHandleAsync(
        PingMessage message, MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
    {
        if (peers.Length is 0 || peers.Contains(messageEnvelope.Sender))
        {
            transport.Post(messageEnvelope.Sender, new PongMessage(), messageEnvelope.Identity);
        }

        return ValueTask.CompletedTask;
    }
}
