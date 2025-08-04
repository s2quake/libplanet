using Libplanet.Net.Messages;

namespace Libplanet.Net.MessageHandlers;

internal sealed class ProtocolRequestMessageHandler(ITransport transport)
    : MessageHandlerBase<ProtocolRequestMessage>
{
    protected override ValueTask OnHandleAsync(
        ProtocolRequestMessage message, MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
    {
        var replyMessage = new ProtocolResponseMessage
        {
            Protocol = transport.Protocol,
        };
        transport.Post(messageEnvelope.Sender, replyMessage, messageEnvelope.ReplyTo);
        return ValueTask.CompletedTask;
    }
}
