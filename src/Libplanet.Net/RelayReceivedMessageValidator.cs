using Libplanet.Net.Messages;

namespace Libplanet.Net;

public sealed class RelayReceivedMessageValidator<T>(Action<T, MessageEnvelope> action) : IReceivedMessageValidator
    where T : IMessage
{
    public RelayReceivedMessageValidator(Action<T> action)
        : this((m, _) => action(m))
    {
    }

    Type IReceivedMessageValidator.MessageType => typeof(T);

    void IReceivedMessageValidator.Validate(MessageEnvelope messageEnvelope)
        => action((T)messageEnvelope.Message, messageEnvelope);
}
