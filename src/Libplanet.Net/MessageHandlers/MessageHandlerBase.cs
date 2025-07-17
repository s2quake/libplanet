using Libplanet.Net.Messages;

namespace Libplanet.Net.MessageHandlers;

internal abstract class MessageHandlerBase<T> : IMessageHandler
    where T : IMessage
{
    Type IMessageHandler.MessageType { get; } = typeof(T);

    protected abstract void OnHandle(T message, MessageEnvelope messageEnvelope);

    void IMessageHandler.Handle(MessageEnvelope messageEnvelope)
        => OnHandle((T)messageEnvelope.Message, messageEnvelope);
}
