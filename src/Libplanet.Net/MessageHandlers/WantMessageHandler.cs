using Libplanet.Net.Messages;

namespace Libplanet.Net.MessageHandlers;

internal sealed class WantMessageHandler(ITransport transport, MessageCollection messages)
    : MessageHandlerBase<WantMessage>
{
    protected override ValueTask OnHandleAsync(
        WantMessage message, MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
    {
        var receiver = messageEnvelope.Sender;
        foreach (var id in message.Ids)
        {
            if (messages.TryGetValue(id, out var existingMessage))
            {
                transport.Post(receiver, existingMessage);
            }
        }

        return ValueTask.CompletedTask;
    }
}
