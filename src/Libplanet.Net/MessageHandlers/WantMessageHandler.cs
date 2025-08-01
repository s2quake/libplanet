using Libplanet.Net.Messages;

namespace Libplanet.Net.MessageHandlers;

internal sealed class WantMessageHandler(ITransport transport, MessageCollection messages)
    : MessageHandlerBase<WantMessage>
{
    protected override void OnHandle(WantMessage message, MessageEnvelope messageEnvelope)
    {
        var receiver = messageEnvelope.Sender;
        foreach (var id in message.Ids)
        {
            if (messages.TryGetValue(id, out var existingMessage))
            {
                transport.Post(receiver, existingMessage);
            }
        }
    }
}
