using Libplanet.Net.Messages;

namespace Libplanet.Net;

public interface IReceivedMessageValidator
{
    Type MessageType { get; }

    void Validate(MessageEnvelope messageEnvelope);
}
