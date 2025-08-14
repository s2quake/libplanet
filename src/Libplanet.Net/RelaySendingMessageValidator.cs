using Libplanet.Net.Messages;

namespace Libplanet.Net;

public sealed class RelaySendingMessageValidator<T>(Action<T, MessageEnvelope> action) : ISendingMessageValidator
    where T : IMessage
{
    public RelaySendingMessageValidator(Action<T> action)
        : this((m, _) => action(m))
    {
    }

    Type ISendingMessageValidator.MessageType => typeof(T);

    void ISendingMessageValidator.Validate(MessageEnvelope messageEnvelope)
        => action((T)messageEnvelope.Message, messageEnvelope);
}
