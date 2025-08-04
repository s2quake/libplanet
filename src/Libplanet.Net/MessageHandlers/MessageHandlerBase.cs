using Libplanet.Net.Messages;

namespace Libplanet.Net.MessageHandlers;

public abstract class MessageHandlerBase<T> : IMessageHandler
    where T : IMessage
{
    Type IMessageHandler.MessageType { get; } = typeof(T);

    protected abstract ValueTask OnHandleAsync(
        T message, MessageEnvelope messageEnvelope, CancellationToken cancellationToken);

    ValueTask IMessageHandler.HandleAsync(MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
        => OnHandleAsync((T)messageEnvelope.Message, messageEnvelope, cancellationToken);
}
