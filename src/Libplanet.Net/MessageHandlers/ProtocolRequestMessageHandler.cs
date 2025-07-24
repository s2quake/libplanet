using Libplanet.Net.Messages;

namespace Libplanet.Net.MessageHandlers;

internal sealed class ProtocolRequestMessageHandler(ITransport transport)
    : MessageHandlerBase<ProtocolRequestMessage>
{
    protected override void OnHandle(ProtocolRequestMessage message, MessageEnvelope messageEnvelope)
    {
        var replyMessage = new ProtocolResponseMessage
        {
            Protocol = transport.Protocol,
        };
        transport.Post(messageEnvelope.Sender, replyMessage, messageEnvelope.ReplyTo);
    }
}
