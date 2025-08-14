using Libplanet.Net.Messages;

namespace Libplanet.Net;

public interface ISendingMessageValidator
{
    Type MessageType { get; }

    void Validate(MessageEnvelope messageEnvelope);
}
