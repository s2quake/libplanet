using Libplanet.Net.Messages;

namespace Libplanet.Net.MessageHandlers;

internal sealed class PingMessageHandler(ITransport transport, params Peer[] peers)
    : MessageHandlerBase<PingMessage>
{
    protected override void OnHandle(PingMessage message, MessageEnvelope messageEnvelope)
    {
        if (peers.Length is 0 || peers.Contains(messageEnvelope.Sender))
        {
            transport.Send(messageEnvelope.Sender, new PongMessage(), messageEnvelope.Identity);
            // await replyContext.PongAsync();
        }
    }
}
