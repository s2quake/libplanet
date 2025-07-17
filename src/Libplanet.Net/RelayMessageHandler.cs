using Libplanet.Net.Messages;

namespace Libplanet.Net;

public sealed class RelayMessageHandler<T>(Action<T, MessageEnvelope> action) : IMessageHandler
    where T : IMessage
{
    public RelayMessageHandler(Action<T> action)
        : this((m, _) => action(m))
    {
    }

    Type IMessageHandler.MessageType => typeof(T);

    void IMessageHandler.Handle(MessageEnvelope messageEnvelope) => action((T)messageEnvelope.Message, messageEnvelope);
}
